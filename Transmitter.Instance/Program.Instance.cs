using System.Net;

using Transmitter.Comms;
using Transmitter.Comms.Gateways;

namespace Transmitter.Instance;

internal static class Program
{
#if DEBUG
    private const string SAVE_FILE_NAME = "state-dev";
    private const ushort port = 57171;
    private const ushort feedbackLinePort = 57172;
#else
    private const string SAVE_FILE_NAME = "state";
    private const ushort port = 57181;
    private const ushort feedbackLinePort = 57182;
#endif

    private static IPAddress? publicIp;

    public static async Task Main()
    {
        PersistentInstanceState state = PersistentInstanceState.LoadOrCreate(Path.Join(Environment.CurrentDirectory, SAVE_FILE_NAME));
        Instance instance = new(port, feedbackLinePort, state);
        instance.Start();

        Console.WriteLine("instance");
        Console.WriteLine("commands");
        Console.WriteLine("@EMIT <public ip>");

        while (true)
        {
            try
            {
                string line = Console.ReadLine() ?? string.Empty;
                string[] split = line.Split();
                string[] args = [.. split.Skip(1)];
                switch (split.FirstOrDefault(string.Empty).ToLowerInvariant())
                {
                    case "@emit":
                    {
                        IPAddress ipAddress = IPAddress.Parse(args[0]);
                        InstanceAddress instanceAddress = new(new WebGatewayAddress(ipAddress, port), new WebGatewayAddress(ipAddress, feedbackLinePort));
                        Guid code = instance.EmitUnboundInviteCode();
                        Console.WriteLine(new InstanceInvite(instanceAddress, code));
                    }
                    break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(line));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}
