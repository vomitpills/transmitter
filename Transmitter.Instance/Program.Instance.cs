using System.Net;

namespace Transmitter.Instance;

internal static class Program
{
#if DEBUG
    private const string SAVE_FILE_NAME = "state-dev";
    private const ushort port = 57171;
    private const ushort returnLinePort = 57172;
#else
    private const string SAVE_FILE_NAME = "state";
    private const ushort port = 57181;
    private const ushort returnLinePort = 57182;
#endif

    private static IPAddress? publicIp;

    public static async Task Main()
    {
        PersistentInstanceState state = PersistentInstanceState.LoadOrCreate(Path.Join(Environment.CurrentDirectory, SAVE_FILE_NAME));
        Instance instance = new(port, returnLinePort, state);
        instance.Start();
        Console.WriteLine(instance.EmitInviteCode());
        Console.WriteLine(instance.EmitInviteCode());
        await Task.Delay(-1);
    }
}
