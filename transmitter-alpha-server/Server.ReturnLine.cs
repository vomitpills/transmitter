using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using transmitter_alpha_common;

namespace transmitter_alpha_server;

partial class Server
{
    private TcpListener? returnLineListener;

    private async Task StartReturnLineLoop(int port)
    {
        if (returnLineListener is not null)
            throw new InvalidOperationException("already running");

        Logger returnLineLogger = new("server-returnline");

        returnLineListener = new(IPAddress.Any, port);
        returnLineListener.Start();

        returnLineLogger.LogProgress("Started listening");

        while (true)
        {
            TcpClient client = await returnLineListener.AcceptTcpClientAsync();
            Logger childLogger = returnLineLogger.CreateChild();
            returnLineLogger.LogProgress($"New connection; delegating to {childLogger.Name}");
            _ = Task.Run(() => AcceptReturnLineConnection(client, childLogger));
        }
    }

    private async Task AcceptReturnLineConnection(TcpClient client, Logger logger)
    {
        Stream stream = client.GetStream();
        Guid peerId;
        while (true)
        {
            try
            {
                OldMessage authRequest = await OldMessage.Deserialize(stream, logger);
                if (authRequest["intent"] is not "check-in")
                    throw new MessageStructureException("intent");

                CheckPeerState(authRequest, out ClientSecret auth);
                AuthenticateReturnLineConnection(auth, authRequest["key"]);
                peerId = persistentState.GetId(auth);

                await new OldMessage()
                {
                    ["status"] = "ok"
                }.Serialize(stream, logger);
                break;
            }
            catch (CommonException e)
            {
                OldMessage errMessage = new()
                {
                    ["status"] = "err",
                    ["cause"] = e.Serialize()
                };
                await errMessage.Serialize(stream, logger);
            }
        }

        lock (returnLineReachablePeers)
            returnLineReachablePeers.Add(peerId);

        SemaphoreSlim semaphore = new(1);
        _ = Task.Run(async () =>
        {
            while (true)
            {
                lock (returnLineReachablePeers)
                    if (!returnLineReachablePeers.Contains(peerId))
                        break;
                await Task.Delay(TimeSpan.FromSeconds(30));
                await SendKeepAlive(stream, logger, semaphore);
            }
        });

        try
        {
            while (true)
            {
                try
                {
                    Transaction transaction = await persistentState.BeginTransactionReadAsync(peerId);
                    OldMessage msg = new()
                    {
                        ["type"] = "mail",
                        ["from"] = transaction.SenderId.ToString(),
                        ["mail"] = JsonSerializer.Serialize(transaction.Mail, OldSerializer.JsonSerializerOptions)
                    };
                    await semaphore.WaitAsync();
                    await msg.Serialize(stream, logger);

                    using CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromSeconds(10));
                    OldMessage response = await OldMessage.Deserialize(stream, logger, cancellationTokenSource.Token);
                    if (response["status"] is "ok")
                        persistentState.AdvanceTransactionQueue(peerId);
                    else
                        throw new InvalidOperationException("Could not deliever mail to receiver");
                }
                finally
                {
                    semaphore.Release();
                }
            }
        }
        catch (CommonException e)
        {
            OldMessage errMessage = new()
            {
                ["status"] = "err",
                ["cause"] = e.Serialize()
            };
            await errMessage.Serialize(stream, logger);
        }
        finally
        {
            logger.LogProgress("Cleaning up");
            client.Dispose();

            lock (returnLineReachablePeers)
                returnLineReachablePeers.Remove(peerId);
        }
    }

    private static async Task SendKeepAlive(Stream stream, Logger logger, SemaphoreSlim semaphore)
    {
        if (semaphore.CurrentCount is 0)
            return;

        semaphore.Wait();
        try
        {
            OldMessage keepAliveRequest = new()
            {
                ["type"] = "ping"
            };
            await keepAliveRequest.Serialize(stream, logger);

            OldMessage keepAliveResponse = await OldMessage.Deserialize(stream, logger);

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
        finally
        {
            semaphore.Release();
        }
    }
}
