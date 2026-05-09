using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using transmitter_alpha_common;

namespace transmitter_alpha_server;

partial class Server
{
    private TcpListener? listener;

    private async Task StartLoop(int port)
    {
        if (listener is not null)
            throw new InvalidOperationException("already running");

        Logger logger = new("server-comm");

        listener = new(IPAddress.Any, port);
        listener.Start();
        logger.LogProgress("Started listening");

        while (true)
        {
            TcpClient client = await listener.AcceptTcpClientAsync();
            Logger childLogger = logger.CreateChild();
            logger.LogProgress($"New connection; delegating to {childLogger.Name}");
            _ = Task.Run(() => AcceptCommConnection(client, childLogger));
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

                    Message response = await Respond(incomingRequest);
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

    private async Task<Message> Respond(Message incomingRequest)
    {
        switch (incomingRequest["intent"])
        {
            case "register":
                {
                    ClientSecret inviteCode = ClientSecret.Decode(incomingRequest["invite-code"]);
                    if (!persistentState.TryRegisterUser(inviteCode, out ClientSecret clientSecret))
                        throw new StateException("invite-code");

                    return new()
                    {
                        ["status"] = "ok",
                        ["secret"] = clientSecret.Encode()
                    };
                }

            case "update-profile":
                {
                    CheckPeerState(incomingRequest, out ClientSecret auth, false);

                    Profile profile = JsonSerializer.Deserialize<Profile>(incomingRequest["data"], Serializer.JsonSerializerOptions) ?? throw new InvalidOperationException();
                    persistentState.UpdateProfile(auth, profile);

                    Guid profileOwnerId = persistentState.GetId(auth);
                    foreach (var peer in persistentState.EnumerateKnownIds()) // run on new thread?
                        await PostUpdate(profileOwnerId, peer, new(MailType.CacheUpdate, JsonSerializer.Serialize(profile)));

                    return new()
                    {
                        ["status"] = "ok"
                    };
                }

            case "check-in":
                {
                    CheckPeerState(incomingRequest, out ClientSecret auth);
                    return new()
                    {
                        ["status"] = "ok",
                        ["key"] = EmitReturnLineAccessCode(auth)
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
                    CheckPeerState(incomingRequest, out ClientSecret auth);

                    Guid senderId = persistentState.GetId(auth);
                    Guid recipient = Guid.Parse(incomingRequest["recipient"]);
                    Mail mail = JsonSerializer.Deserialize<Mail>(incomingRequest["mail"], Serializer.JsonSerializerOptions) ?? throw new FaultyDataException("mail");

                    if (await PostUpdate(senderId, recipient, mail))
                        return new()
                        {
                            ["status"] = "ok"
                        };
                    throw new CommonException("internal", "record-transaction");
                }

            case "get-peers":
                {
                    CheckPeerState(incomingRequest, out ClientSecret auth);
                    Guid id = persistentState.GetId(auth);

                    return new()
                    {
                        ["status"] = "ok",
                        ["ids"] = string.Join('|', persistentState.EnumerateKnownIds().Where(i => id != i))
                    };
                }

            case "get-profile":
                {
                    CheckPeerState(incomingRequest, out _);

                    Guid id = Guid.Parse(incomingRequest["id"]);
                    if (persistentState.GetProfile(id) is not Profile profile)
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

    private async Task<bool> PostUpdate(Guid sender, Guid receiver, Mail mail)
    {
        if (receiver == Guid.Empty)
        {
            bool success = true;
            foreach (var id in persistentState.EnumerateKnownIds().Where(i => i != sender))
                success &= await PostUpdate(sender, id, mail);
            return success;
        }

        if (!persistentState.UserExists(receiver))
            throw new StateException("recipient");
        return persistentState.RecordPendingTransaction(new(mail, sender), receiver);
    }
}