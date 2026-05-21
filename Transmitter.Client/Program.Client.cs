using System.Net;

using Transmitter.Comms;
using Transmitter.Comms.MailContents;

namespace Transmitter.Client;

internal static class Program
{
#if DEBUG
    private const string CONFIG_NAME = "config-03.debug";
#else
    private const string CONFIG_NAME = "config-03";
#endif

#if DEBUG
    private const ushort port = 57171;
    private const ushort returnLinePort = 57172;
#else
    private const ushort port = 57181;
    private const ushort returnLinePort = 57182;
#endif

    private const int RECONNECT_ATTEMPTS = 5;
    private static readonly TimeSpan reconnectInterval = TimeSpan.FromSeconds(5);

    public static async Task Main()
    {
        Profile profile = new("test-1", ConsoleColor.Green, (Profile.BeepRange.From + Profile.BeepRange.To) / 2);
        Client client = new(profile);
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