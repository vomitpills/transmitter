using System.Net;

namespace Transmitter.Instance;

internal static class Program
{
#if DEBUG
    private const ushort port = 57171;
    private const ushort returnLinePort = 57172;
#else
    private const ushort port = 57181;
    private const ushort returnLinePort = 57182;
#endif

    private static IPAddress? publicIp;

    public static async Task Main()
    {
        Instance instance = new(port, returnLinePort);
        instance.Start();
        Console.WriteLine(instance.EmitInviteCode());
        Console.WriteLine(instance.EmitInviteCode());
        await Task.Delay(-1);
    }
}
