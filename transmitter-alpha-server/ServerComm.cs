using System.Data;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
using transmitter_alpha_common;

namespace transmitter_alpha_server;

partial class Server
{
    private TcpListener? commListener;

    private async Task StartCommLoop(int port)
    {
        if (commListener is not null)
            throw new InvalidOperationException("already running");

        Logger commLogger = new("server-comm");

        commListener = new(IPAddress.Any, port);
        commListener.Start();
        commLogger.LogProgress("Started listening");

        while (true)
        {
            TcpClient client = await commListener.AcceptTcpClientAsync();
            Logger childLogger = commLogger.CreateChild();
            commLogger.LogProgress($"New connection; delegating to {childLogger.Name}");

            Task connectionTask = Task.Run(() => AcceptCommConnection(client, childLogger));
            lock (peerCommConnections)
                peerCommConnections.Add(connectionTask);

            _ = Task.Run(() => connectionTask.ContinueWith(async (t) =>
            {
                childLogger.LogProgress("Cleaning up");
                lock (peerCommConnections)
                    peerCommConnections.Remove(t);
            }));
        }
    }

    private async Task AcceptCommConnection(TcpClient client, Logger logger)
    {
        Stream stream = client.GetStream();
        try
        {
            while (client.Connected)
            {
                try
                {
                    Message incomingRequest = await Message.Deserialize(stream, logger);
                    Message response = await RespondComm(incomingRequest);
                    await response.Serialize(stream, logger);
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
            }
        }
        finally
        {
            logger.LogAbort("comm connection");
            client.Dispose();
        }
    }

    private async Task<Message> RespondComm(Message incomingRequest)
    {
        switch (incomingRequest["intent"])
        {
            case "get-id":
                {
                    //string inviteCode = incomingRequest["invite-code"];

                    //if (!validInvites.Remove(inviteCode))
                    //    throw new FaultyDataException("invite-code");

                    string secret = GetNewSecret();
                    string id = GetNewId();
                    peerSecretToId[secret] = id;
                    if (!backlog.ContainsKey(id))
                        backlog[id] = Channel.CreateBounded<Transaction>(64);

                    return new()
                    {
                        ["status"] = "ok",
                        ["secret"] = secret,
                        ["id"] = id
                    };
                }

            case "update-profile":
                {
                    CheckPeerState(incomingRequest, out string auth, false);

                    Profile profile = JsonSerializer.Deserialize<Profile>(incomingRequest["data"], Serializer.JsonSerializerOptions) ?? throw new InvalidOperationException();
                    string id = peerSecretToId[auth];
                    profiles[id] = profile;

                    return new()
                    {
                        ["status"] = "ok"
                    };
                }

            case "check-in":
                {
                    CheckPeerState(incomingRequest, out string auth);

                    string key = RandomNumberGenerator.GetHexString(16);

                    lock (differedChannelKeysAndSecrets)
                        differedChannelKeysAndSecrets[key] = auth;

                    return new()
                    {
                        ["status"] = "ok",
                        ["key"] = key
                    };
                }

            case "ping":
                {
                    CheckPeerState(incomingRequest, false);
                    return new()
                    {
                        ["status"] = "pong"
                    };
                }

            case "send-mail":
                {
                    CheckPeerState(incomingRequest, out string auth);

                    string senderId = peerSecretToId[auth];
                    string recipient = incomingRequest["recipient"];

                    Mail mail = JsonSerializer.Deserialize<Mail>(incomingRequest["mail"], Serializer.JsonSerializerOptions) ?? throw new FaultyDataException("mail");

                    (MailDelieveryStatus status, Transaction? transaction) = await PostUpdate(senderId, recipient, mail);

                    switch (status)
                    {
                        case MailDelieveryStatus.Delievered:
                            return new()
                            {
                                ["status"] = "ok"
                            };

                        case MailDelieveryStatus.Cached:
                            {
                                if (transaction is null)
                                    throw new InvalidOperationException();

                                lock (transaction)
                                {
                                    transaction.SetCallback(async () =>
                                    {
                                        await PostUpdate(recipient, senderId, new(MailType.Receipt, string.Empty));
                                    });

                                    return new()
                                    {
                                        ["status"] = "cached",
                                        ["transaction-id"] = transaction.Id
                                    };
                                }
                            }

                        default:
                            throw new StateException("internal/mail-status");
                    }
                }

            case "get-peers":
                {
                    CheckPeerState(incomingRequest, out _);

                    return new()
                    {
                        ["status"] = "ok",
                        ["ids"] = string.Join('|', peerDifferedConnections)
                    };
                }

            case "get-profile":
                {
                    CheckPeerState(incomingRequest, out _);

                    string id = incomingRequest["id"];
                    if (!profiles.TryGetValue(id, out Profile? profile))
                        throw new StateException("id");

                    return new()
                    {
                        ["status"] = "ok",
                        ["profile"] = JsonSerializer.Serialize(profile, Serializer.JsonSerializerOptions)
                    };
                }

            default:
                throw new MessageStructureException("intent");
        }
    }
}

internal enum MailDelieveryStatus
{
    Delievered, Cached
}