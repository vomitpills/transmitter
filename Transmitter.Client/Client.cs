using System.Net.Sockets;
using System.Text.Json;
using transmitter_common;

namespace Transmitter.Client;

public class Client(ClientBootstrapInfo bootstrapInfo, Profile profile) : IDisposable
{
    public Profile Profile { get; private set; } = profile;
    private TcpClient? commClient;
    private TcpClient? returnLineClient;

    private readonly SemaphoreSlim commSemaphore = new(1);

    private readonly Dictionary<Guid, Profile> profileCache = [];

    private ClientBootstrapInfo BootstrapInfo
    {
        get;
        set
        {
            field = value;
            BootstrapInfoUpdated?.Invoke(value);
        }
    } = bootstrapInfo;

    public event Action<ClientBootstrapInfo>? BootstrapInfoUpdated;

    private Task? returnLineChannelTask;

    private Stream? commStream;
    private Logger? commLogger;

    public async Task Start()
    {
        commClient = new();
        commLogger = new("client-comm");
        await commClient.ConnectAsync(new(BootstrapInfo.ServerAddress, BootstrapInfo.CommPort));
        commStream = commClient.GetStream();
        commLogger.LogProgress("Connected comm socket");

        await EnsureClientSecret(commStream, commLogger);
        await Connect(commStream, commLogger);

        _ = Task.Run(async() =>
        {
            while (commClient.Connected)
            {
                await Task.Delay(TimeSpan.FromSeconds(30));
                await SendKeepAlive(commStream);
            }
        });
    }

    public async Task AwaitAllTasks(CancellationToken cancellationToken = default)
    {
        if (returnLineChannelTask is not null)
            await returnLineChannelTask.WaitAsync(cancellationToken);
    }

    public async Task Stop()
    {
        commClient?.Dispose();
        commClient = null;
        returnLineClient?.Dispose();
        returnLineClient = null;
    }

    public async Task Pause()
    {
        await Stop();
    }

    public async Task Unpause()
    {
        await Start();
    }

    private async Task SendKeepAlive(Stream stream)
    {
        if (commSemaphore.CurrentCount is 0)
            return;

        commSemaphore.Wait();
        try
        {
            OldMessage keepAliveRequest = WithAuth(new()
            {
                ["intent"] = "ping"
            });
            await keepAliveRequest.Serialize(stream, null);

            OldMessage keepAliveResponse = await OldMessage.Deserialize(stream, null);

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
            commSemaphore.Release();
        }
    }

    private async Task EnsureClientSecret(Stream stream, Logger logger)
    {
        if (BootstrapInfo.ClientSecret.IsAuthToken)
            return;

        Request registerRequest = new()
        {
            ["intent"] = "register",
            ["invite-code"] = BootstrapInfo.ClientSecret.Encode()
        };
        await registerRequest.Serialize(stream, logger);

        OldMessage getIdResponse = await OldMessage.Deserialize(stream, logger);
        BootstrapInfo = getIdResponse["status"] switch
        {
            "ok" => BootstrapInfo with { ClientSecret = ClientSecret.Decode(getIdResponse["secret"]) },
            "err" => throw CommonException.Deserialize(getIdResponse["cause"]),
            _ => throw new MessageStructureException("status")
        };
        logger.LogProgress("Obtained auth token");
    }

    private async Task Connect(Stream stream, Logger logger, bool allowRecursion = true)
    {
        OldMessage checkInRequest = WithAuth(new()
        {
            ["intent"] = "check-in"
        });
        await checkInRequest.Serialize(stream, logger);

        OldMessage checkInResponse = await OldMessage.Deserialize(stream, logger);
        switch (checkInResponse["status"])
        {
            case "ok":
                logger.LogProgress("Checked in");
                string key = checkInResponse["key"];
                await ConnectReturnLineStream(key);
                break;

            case "err":
                CommonException exception = CommonException.Deserialize(checkInResponse["cause"]);
                if (!allowRecursion || exception.Specifier is not "state" || exception.Fault is not "no-profile")
                    throw exception;
                await UpdateProfile(Profile, logger);
                await Connect(stream, logger, false);
                break;

            default:
                throw new MessageStructureException("status");
        }
    }

    private async Task ConnectReturnLineStream(string key)
    {
        if (returnLineChannelTask is not null)
            throw new InvalidOperationException("already connected");

        Logger returnLineLogger = new("client-returnline");

        returnLineClient = new();
        await returnLineClient.ConnectAsync(new(BootstrapInfo.ServerAddress, BootstrapInfo.ReturnLinePort));
        Stream stream = returnLineClient.GetStream();
        returnLineLogger.LogProgress("Connected returnline socket");

        OldMessage checkInRequest = WithAuth(new()
        {
            ["intent"] = "check-in",
            ["key"] = key
        });
        await checkInRequest.Serialize(stream, returnLineLogger);

        OldMessage response = await OldMessage.Deserialize(stream, returnLineLogger);
        if (response["status"] is not "ok")
            throw new InvalidOperationException();

        returnLineLogger.LogProgress("Authorized");

        returnLineChannelTask = Task.Run(async () =>
        {
            try
            {
                while (returnLineClient.Connected)
                {
                    OldMessage incomingMessage = await OldMessage.Deserialize(stream, null);

                    switch (incomingMessage["type"])
                    {
                        case "ping":
                            {
                                await new OldMessage()
                                {
                                    ["status"] = "pong"
                                }.Serialize(stream, null);
                                break;
                            }

                        case "mail":
                            {
                                returnLineLogger.Write($"{returnLineLogger.Name}: {JsonSerializer.Serialize(OldMessage.Censor(incomingMessage.ToDictionary()), OldMessage.VisualSerializerOptions)}", ConsoleColor.Gray);

                                OldMail mail = JsonSerializer.Deserialize<OldMail>(incomingMessage["mail"], OldSerializer.JsonSerializerOptions) ?? throw new InvalidOperationException();
                                await new OldMessage()
                                {
                                    ["status"] = "ok"
                                }.Serialize(stream, returnLineLogger);

                                Guid senderId = Guid.Parse(incomingMessage["from"]);
                                switch (mail.MailType)
                                {
                                    case MailType.Message:
                                        Logger.LogMessage(await GetProfile(senderId, returnLineLogger), mail.Data);
                                        break;

                                    case MailType.CacheUpdate:
                                        Profile newProfile = JsonSerializer.Deserialize<Profile>(mail.Data, OldSerializer.JsonSerializerOptions) ?? throw new InvalidOperationException();
                                        if (profileCache.TryGetValue(senderId, out Profile? oldProfile))
                                        {
                                            returnLineLogger.LogProgress("Received profile update:");
                                            Logger.LogProfileUpdate(oldProfile, newProfile);
                                        }
                                        profileCache[senderId] = newProfile;
                                        break;

                                    default:
                                        throw new NotImplementedException();
                                }
                                break;
                            }

                        default:
                            throw new InvalidOperationException();
                    }
                }
            }
            finally
            {
                returnLineChannelTask = null;
            }
        });
    }

    public Task UpdateProfile(Profile profile) => UpdateProfile(profile, commLogger!);

    private async Task UpdateProfile(Profile profile, Logger logger)
    {
        if (commStream is null)
            throw new InvalidOperationException();

        await commSemaphore.WaitAsync();
        try
        {
            OldMessage updateProfileRequest = WithAuth(new()
            {
                ["intent"] = "update-profile",
                ["data"] = JsonSerializer.Serialize(profile, OldSerializer.JsonSerializerOptions),
            });
            await updateProfileRequest.Serialize(commStream, logger);

            OldMessage updateProfileResponse = await OldMessage.Deserialize(commStream, logger);
            Profile = updateProfileResponse["status"] switch
            {
                "ok" => profile,
                "err" => throw CommonException.Deserialize(updateProfileResponse["cause"]),
                _ => throw new MessageStructureException("status"),
            };
        }
        finally
        {
            commSemaphore.Release();
        }
    }

    private OldMessage WithAuth(Dictionary<string, string> data)
    {
        if (!BootstrapInfo.ClientSecret.IsAuthToken)
            throw new InvalidOperationException("No auth token");
        return [.. data.Append(new("auth", BootstrapInfo.ClientSecret.Encode()))];
    }

    public async Task SendMail(OldMail mail)
    {
        if (commStream is null || commLogger is null)
            throw new InvalidOperationException("Comm stream not initialized");

        await commSemaphore.WaitAsync();
        try
        {
            OldMessage sendMailRequest = WithAuth(new()
            {
                ["intent"] = "send-mail",
                ["recipient"] = Guid.Empty.ToString(),
                ["mail"] = JsonSerializer.Serialize(mail, OldSerializer.JsonSerializerOptions)
            });
            await sendMailRequest.Serialize(commStream, commLogger);

            OldMessage sendMailResponse = await OldMessage.Deserialize(commStream, commLogger);
            switch (sendMailResponse["status"])
            {
                case "ok":
                    break;
                case "cached":
                    throw new NotImplementedException();
                case "err":
                    throw CommonException.Deserialize(sendMailResponse["cause"]);
                default:
                    throw new MessageStructureException("status");
            }
        }
        finally
        {
            commSemaphore.Release();
        }
    }

    private async Task<Profile> GetProfile(Guid id, Logger logger)
    {
        if (commStream is null)
            throw new InvalidOperationException();

        if (profileCache.TryGetValue(id, out Profile? cached))
            return cached;

        await commSemaphore.WaitAsync();
        try
        {
            OldMessage request = WithAuth(new()
            {
                ["intent"] = "get-profile",
                ["id"] = id.ToString()
            });
            await request.Serialize(commStream, logger);

            OldMessage response = await OldMessage.Deserialize(commStream, logger);
            Profile profile = JsonSerializer.Deserialize<Profile>(response["profile"], OldSerializer.JsonSerializerOptions) ?? throw new FaultyDataException("profile");
            profileCache[id] = profile;
            return response["status"] switch
            {
                "ok" => profile,
                "err" => throw CommonException.Deserialize(response["cause"]),
                _ => throw new MessageStructureException("status")
            };
        }
        finally
        {
            commSemaphore.Release();
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        commClient?.Dispose();
        returnLineClient?.Dispose();
    }
}