using System.Collections.Specialized;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication.ExtendedProtection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using transmitter_alpha_common;

namespace transmitter_alpha_server;

partial class Server
{
    private TcpListener? updateListener;

    private async Task StartUpdateLoop(int port)
    {
        if (updateListener is not null)
            throw new InvalidOperationException("already running");

        Logger notifLogger = new("server-notif");

        updateListener = new(IPAddress.Any, port);
        updateListener.Start();

        notifLogger.LogProgress("Started listening");

        while (true)
        {
            TcpClient client = await updateListener.AcceptTcpClientAsync();
            Logger childLogger = notifLogger.CreateChild();
            notifLogger.LogProgress($"New connection; delegating to {childLogger.Name}");

            Task connectionTask = Task.Run(() => AcceptUpdateConnection(client, childLogger));

            _ = Task.Run(() => connectionTask.ContinueWith(async (t) =>
            {
                childLogger.LogProgress("Cleaning up");
                client.Dispose();
            }));
        }
    }

    private async Task AcceptUpdateConnection(TcpClient client, Logger logger)
    {
        Stream stream = client.GetStream();
        string? id = null;
        try
        {
            Message incomingRequest = await Message.Deserialize(stream, logger);

            string auth = AuthorizeUpdateConnection(incomingRequest);
            id = peerSecretToId[auth];

            await new Message()
            {
                ["status"] = "ok"
            }.Serialize(stream, logger);

            lock (peerDifferedConnections)
                peerDifferedConnections.Add(id);

            var channel = backlog[id];
            while (true)
            {
                try
                {
                    using CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromSeconds(30));
                    if (!await channel.Reader.WaitToReadAsync(cancellationTokenSource.Token))
                        break;

                    if (!channel.Reader.TryPeek(out Transaction? transaction))
                        throw new InvalidOperationException();

                    Message update = new()
                    {
                        ["type"] = "mail",
                        ["from"] = transaction.SenderId,
                        ["mail"] = JsonSerializer.Serialize(transaction.Mail, Serializer.JsonSerializerOptions)
                    };
                    await update.Serialize(stream, logger);

                    Message response = await Message.Deserialize(stream, logger);
                    if (response["status"] is not "ok")
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30));
                        continue;
                    }

                    if (!channel.Reader.TryRead(out _))
                        throw new InvalidOperationException();
                    transaction.Callback?.Invoke();
                }
                catch (OperationCanceledException)
                {
                    logger.LogAbort("read; sending keep-alive");
                    await SendKeepAlive(stream, logger);
                }
            }
        }
        catch (CommonException e)
        {
            Message errMessage = new()
            {
                ["status"] = "err",
                ["cause"] = e.Serialize()
            };
            await errMessage.Serialize(stream, logger);
        }
        finally
        {
            if (id is not null)
                lock (peerDifferedConnections)
                    peerDifferedConnections.Remove(id);
        }
    }

    private async Task<(MailDelieveryStatus status, Transaction? transaction)> PostUpdate(string from, string to, Mail mail)
    {
        if (!KnownPeerIds.Contains(to))
            throw new StateException("recipient");

        var forwardChannel = backlog[to];
        var backwardChannel = backlog[to];

        Transaction transaction = new(from, mail);

        TaskCompletionSource source = new();
        transaction.SetCallback(async () =>
        {
            source.SetResult();
        });
        await forwardChannel.Writer.WriteAsync(transaction);

        if (peerDifferedConnections.Contains(to))
        {
            await source.Task.WaitAsync(TimeSpan.FromSeconds(10));
            if (source.Task.IsCompleted)
                return (MailDelieveryStatus.Delievered, null);
        }
        return (MailDelieveryStatus.Cached, transaction);
    }

    private enum UpdateStatus
    {
        Cached, Received
    }

    private static async Task SendKeepAlive(Stream stream, Logger logger)
    {
        Message keepAliveRequest = new()
        {
            ["type"] = "ping"
        };
        await keepAliveRequest.Serialize(stream, logger);

        Message keepAliveResponse = await Message.Deserialize(stream, logger);

        switch (keepAliveResponse["status"])
        {
            case "pong":
                break;
            case "err":
                throw CommonException.Deserialize(keepAliveResponse["cause"]);
            default:
                throw new MessageStructureException("status");
        }
    }

    private string AuthorizeUpdateConnection(Message incomingRequest)
    {
        if (incomingRequest["intent"] is not "check-in")
            throw new MessageStructureException("intent");

        string key = incomingRequest["key"];
        string auth = incomingRequest["auth"];

        lock (differedChannelKeysAndSecrets)
            if (!differedChannelKeysAndSecrets.Remove(key, out string? secret) || secret != auth)
                throw new FaultyDataException("key");

        return auth;
    }

    private static string NewTransactionId() => Guid.NewGuid().ToString();
}
