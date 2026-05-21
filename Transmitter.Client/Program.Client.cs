using System.Net;

using Transmitter.Comms.MailContents;

namespace Transmitter.Client;

internal static class Program
{
#if DEBUG
    private const string SAVE_FILE_NAME = "config-03-dev";
    private const ushort port = 57171;
    private const ushort returnLinePort = 57172;
#else
    private const string SAVE_FILE_NAME = "config-03";
    private const ushort port = 57181;
    private const ushort returnLinePort = 57182;
#endif

    private const int RECONNECT_ATTEMPTS = 5;
    private static readonly TimeSpan reconnectInterval = TimeSpan.FromSeconds(5);

    public static async Task Main()
    {
        PersistentClientState state = PersistentClientState.LoadOrCreate(Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "transmitter", SAVE_FILE_NAME));
        Client client = new(state);

        Guid inviteCode = Guid.Parse(Console.ReadLine()!);
        IChat chat = await client.ConnectToInstance(IPAddress.Loopback, port, returnLinePort, inviteCode);

        Logger temp = new("client");

        Console.WriteLine("press 'r' to start receiving");
        bool isReceiving = Console.ReadKey(true).Key is ConsoleKey.R;

        if (isReceiving)
        {
            chat.MailReceived += (transaction) =>
            {
                string msg = $"({transaction.GetType()})";
                foreach (var item in transaction.Mail.Content)
                    msg += $" [{item.GetType()}:{item}]";
                temp.Log(msg);
            };
            await Task.Delay(Timeout.Infinite);
        }
        else
            while (true)
            {
                Console.Write("msg: ");
                string message = Console.ReadLine()!;
                Console.Write("rid: ");
                Guid guid = Guid.Parse(Console.ReadLine()!);
                await chat.PostMail(new(new TextMailContent(message)), guid);
            }
    }
}