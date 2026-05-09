using System.Net;
using transmitter_alpha_common;

namespace transmitter_alpha_server;

internal static class Program
{
    private const ushort port = 57181;
    private const ushort returnLinePort = 57182;
    private static IPAddress? publicIp;

    public static async Task Main()
    {
        Server server = new();
        server.Start(port, returnLinePort);

        Logger.EnableInput();

        while (true)
        {
            if (Logger.LogInput(Console.ReadKey(true)) is not string command)
                continue;

            var split = command.Split(' ');
            switch (split[0])
            {
                case "emit":
                    if (publicIp is null)
                    {
                        Logger.LogInputError("ip not set. use 'meta <ip>'");
                        break;
                    }
                    string bootstrapCode = new ClientBootstrapInfo(publicIp, port, returnLinePort, server.EmitInviteCode()).Encode();
                    Logger.LogNote("bootstrap code", bootstrapCode, ConsoleColor.Cyan);
                    break;

                case "meta":
                    try
                    {
                        publicIp = IPAddress.Parse(split[1]);
                        Logger.LogNote(null, $"ip set to {publicIp}", ConsoleColor.Cyan);
                    }
                    catch
                    {
                        Logger.LogInputError("parsing error");
                    }
                    break;

                default:
                    Logger.LogInputError("unknown command");
                    break;
            }
        }
    }
}
