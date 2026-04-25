using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Channels;
using transmitter_alpha_common;

namespace transmitter_alpha_client;

internal class CommandException(string message) : Exception(message) { }

internal static class Program
{
    public static async Task Main()
    {
        Console.WriteLine();
        Logger.LogInputNote("* connect <ip> <commPort> <notifPort> <displayName>");
        Logger.EnableHighlight();
        Logger.EnableInput();

        Func<Task>? commandTask = null;
        while (commandTask is null)
        {
            var key = Console.ReadKey(true);
            if (Logger.LogInput(key) is not string command)
                continue;

            try
            {
                commandTask = ParseCommand(command);
            }
            catch (Exception e)
            {
                if (e is CommandException commandException)
                    Logger.LogInputError(commandException.Message);
                else
                    Logger.LogInputError(e.ToString(), "runtime error");
            }
        }

        try
        {
            Logger.DisableHighlight();
            Logger.DisableInput();
            await commandTask();
            Logger.DisableInput();
        }
        catch (Exception e)
        {
            Logger.LogInputError($"Something went wrong, and a crash has occured.", "Uh-oh!");
            Logger.LogInputError(e.ToString(), "Trace");

            Logger.LogInputNote("You can now close the window");
            await Task.Delay(Timeout.Infinite);
        }
    }

    private static Func<Task> ParseCommand(string command)
    {
        string[] split = [.. command.Split(' ').Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim('\0'))];

        if (split.Length <= 0)
            throw new CommandException("no verb specified");

        string GetArgument(int pos, string name)
        {
            if (split.Length < ++pos + 1)
                throw new CommandException($"expected <{name}> at pos {pos}");
            return split[pos];
        }

        string verb = split[0];
        switch (verb.ToLowerInvariant())
        {
            case "connect":
                {
                    if (split.Length > 1 + 4)
                        throw new CommandException("too many arguments");
                    if (split.Length < 1 + 4)
                        throw new CommandException("not enough arguments");

                    IPAddress ipAddress = IPAddress.Parse(GetArgument(0, "ip"));
                    int commPort = int.Parse(GetArgument(1, "commPort"));
                    int notifPort = int.Parse(GetArgument(2, "notifPort"));
                    string displayName = GetArgument(3, "displayName");

                    return async () => await Connect(ipAddress, commPort, notifPort, displayName);
                }

            default:
                throw new CommandException($"unknown verb '{verb}'");
        }
    }

    // connect 128.0.134.58 57181 57182 test

    private static async Task Connect(IPAddress ipAddress, int commPort, int notifPort, string displayName)
    {
        try
        {
            Client testClient = new(new(ipAddress, commPort), new(ipAddress, notifPort), new(displayName));
            await testClient.Start();
            await Task.Delay(Timeout.Infinite);
        }
        finally
        {
            Logger.DisableInput();
        }
    }
}

public class Client(IPEndPoint serverCommEndpoint, IPEndPoint serverDifferedEndpoint, Profile profile)
{
    private readonly IPEndPoint serverCommEndpoint = serverCommEndpoint;
    private readonly IPEndPoint serverDifferedEndpoint = serverDifferedEndpoint;
    private readonly Profile profile = profile;
    private readonly TcpClient commClient = new() { };
    private readonly TcpClient differedClient = new() { };
    private bool isConnected = false;

    private readonly Dictionary<string, Profile> profileCache = [];

    private Task? updateChannelTask;

    private string? clientSecret;
    private string? clientId;

    public async Task Start()
    {
        Logger commLogger = new("client-comm");
        await commClient.ConnectAsync(serverCommEndpoint);
        Stream stream = commClient.GetStream();
        commLogger.LogProgress("Connected comm socket");

        if (clientSecret is null)
        {
            await GetClientSecret(stream, commLogger);
            commLogger.LogProgress("Obtained client secret");
        }
        isConnected = true;

        await CheckIn(stream, commLogger);

        Logger.EnableInput();

        Channel<ConsoleKeyInfo> consoleInput = Channel.CreateBounded<ConsoleKeyInfo>(64);
        Task consoleInputTask = Task.Run(async () =>
        {
            while (true)
            {
                ConsoleKeyInfo consoleKeyInfo = Console.ReadKey(true);
                await consoleInput.Writer.WriteAsync(consoleKeyInfo);
            }
        });

        while (true)
        {
            using CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromSeconds(30));
            try
            {
                while (!cancellationTokenSource.IsCancellationRequested)
                {
                    ConsoleKeyInfo consoleKeyInfo = await consoleInput.Reader.ReadAsync(cancellationTokenSource.Token);

                    if (Logger.LogInput(consoleKeyInfo) is not string message)
                        continue;

                    Mail mail = new(MailType.Message, message);
                    await SendMail(stream, null, mail, commLogger);

                    Logger.LogMessage("<you>", message, false);
                }
            }
            catch (OperationCanceledException)
            {
                commLogger.LogAbort("read; sending keep-alive");
                await SendKeepAlive(stream, commLogger);
            }
        }
    }

    private async Task SendKeepAlive(Stream stream, Logger logger)
    {
        Message keepAliveRequest = WithAuth(new()
        {
            ["intent"] = "ping"
        });
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

    private async Task GetClientSecret(Stream stream, Logger logger)
    {
        Message getIdRequest = new()
        {
            ["intent"] = "get-id",
            ["invite-code"] = "none"
        };
        await getIdRequest.Serialize(stream, logger);

        Message getIdResponse = await Message.Deserialize(stream, logger);
        (clientSecret, clientId) = getIdResponse["status"] switch
        {
            "ok" => (getIdResponse["secret"], getIdResponse["id"]),
            "err" => throw CommonException.Deserialize(getIdResponse["cause"]),
            _ => throw new MessageStructureException("status")
        };
    }

    private async Task CheckIn(Stream stream, Logger logger, bool allowRecursion = true)
    {
        Message checkInRequest = WithAuth(new()
        {
            ["intent"] = "check-in"
        });
        await checkInRequest.Serialize(stream, logger);

        Message checkInResponse = await Message.Deserialize(stream, logger);
        switch (checkInResponse["status"])
        {
            case "ok":
                logger.LogProgress("Checked in");
                string key = checkInResponse["key"];
                await ConnectUpdateStream(key);
                break;

            case "err":
                CommonException exception = CommonException.Deserialize(checkInResponse["cause"]);
                if (!allowRecursion || exception.Specifier is not "state" || exception.Fault is not "no-profile")
                    throw exception;
                await UpdateProfile(stream, logger);
                await CheckIn(stream, logger, false);
                break;

            default:
                throw new MessageStructureException("status");
        }

    }

    private async Task ConnectUpdateStream(string key)
    {
        if (updateChannelTask is not null)
            throw new InvalidOperationException("already connected");

        Logger notifLogger = new("client-notif");

        await differedClient.ConnectAsync(serverDifferedEndpoint);
        Stream stream = differedClient.GetStream();
        notifLogger.LogProgress("Connected notif socket");

        Message checkInRequest = WithAuth(new()
        {
            ["intent"] = "check-in",
            ["key"] = key
        });
        await checkInRequest.Serialize(stream, notifLogger);

        Message response = await Message.Deserialize(stream, notifLogger);
        if (response["status"] is not "ok")
            throw new InvalidOperationException();

        notifLogger.LogProgress("Authorized");

        updateChannelTask = Task.Run(async () =>
        {
            while (true)
            {
                Message update = await Message.Deserialize(stream, notifLogger);

                switch (update["type"])
                {
                    case "ping":
                        {
                            await new Message()
                            {
                                ["status"] = "pong"
                            }.Serialize(stream, notifLogger);
                            break;
                        }
                    case "mail":
                        {
                            Mail mail = JsonSerializer.Deserialize<Mail>(update["mail"], Serializer.JsonSerializerOptions) ?? throw new InvalidOperationException();
                            await new Message()
                            {
                                ["status"] = "ok"
                            }.Serialize(stream, notifLogger);

                            string senderId = update["from"];

                            switch (mail.MailType)
                            {
                                case MailType.Message:
                                    Logger.LogMessage(senderId, mail.Data);
                                    break;

                                case MailType.Receipt:
                                    Logger.LogReceipt(senderId, mail.Data);
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
        });
    }

    private async Task UpdateProfile(Stream stream, Logger logger)
    {
        Message updateProfileRequest = WithAuth(new()
        {
            ["intent"] = "update-profile",
            ["data"] = JsonSerializer.Serialize(profile, Serializer.JsonSerializerOptions),
        });
        await updateProfileRequest.Serialize(stream, logger);

        Message updateProfileResponse = await Message.Deserialize(stream, logger);
        switch (updateProfileResponse["status"])
        {
            case "ok":
                break;
            case "err":
                throw CommonException.Deserialize(updateProfileResponse["cause"]);
            default:
                throw new MessageStructureException("status");
        }
    }

    private Message WithAuth(Dictionary<string, string> data) => [.. data.Append(new("auth", clientSecret ?? throw new InvalidOperationException("no client secret")))];

    public async Task SendMail(Stream stream, string? recipientId, Mail mail, Logger logger)
    {
        if (recipientId is null)
        {
            foreach (var item in await GetPeers(stream, logger))
                await SendMail(stream, item, mail, logger);
            return;
        }

        Message sendMailRequest = WithAuth(new()
        {
            ["intent"] = "send-mail",
            ["recipient"] = recipientId,
            ["mail"] = JsonSerializer.Serialize(mail, Serializer.JsonSerializerOptions)
        });
        await sendMailRequest.Serialize(stream, logger);

        Message sendMailResponse = await Message.Deserialize(stream, logger);
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

    private async Task<IEnumerable<string>> GetPeers(Stream stream, Logger logger)
    {
        Message request = WithAuth(new()
        {
            ["intent"] = "get-peers"
        });
        await request.Serialize(stream, logger);

        Message response = await Message.Deserialize(stream, logger);
        return response["status"] switch
        {
            "ok" => response["ids"].Split('|').Where(id => id != clientId),
            "err" => throw CommonException.Deserialize(response["cause"]),
            _ => throw new MessageStructureException("status")
        };
    }

    private async Task<Profile> GetProfile(Stream stream, string id, Logger logger)
    {
        if (profileCache.TryGetValue(id, out Profile? cached))
            return cached;

        Message request = WithAuth(new()
        {
            ["intent"] = "get-profile",
            ["id"] = id
        });
        await request.Serialize(stream, logger);

        Message response = await Message.Deserialize(stream, logger);
        Profile profile = JsonSerializer.Deserialize<Profile>(response["profile"], Serializer.JsonSerializerOptions) ?? throw new FaultyDataException("profile");
        profileCache[id] = profile;
        return response["status"] switch
        {
            "ok" => profile,
            "err" => throw CommonException.Deserialize(response["cause"]),
            _ => throw new MessageStructureException("status")
        };
    }
}