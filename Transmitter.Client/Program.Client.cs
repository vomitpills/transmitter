using CLI;

using Transmitter.Comms;
using Transmitter.Comms.MailContents;
using Transmitter.Comms.Requests;

namespace Transmitter.Client;

internal static partial class Program
{
#if DEBUG
    private const string SAVE_FILE_NAME = "config-04-dev";
#else
    private const string SAVE_FILE_NAME = "config-04";
#endif

    private const int RECONNECT_ATTEMPTS = 5;
    private static readonly TimeSpan reconnectInterval = TimeSpan.FromSeconds(5);

    public static async Task Main()
    {
        PersistentClientState state = PersistentClientState.FromPath(Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "transmitter", SAVE_FILE_NAME), Profile.RandomProfile);
        Client client = new(state);

        Console.WriteLine();
        Logger.EnableInput();

        Guid? selectedChatOrUser = default;
        bool isUserSelected = default;

        PrintHelp();
        PrintChats(state, selectedChatOrUser, isUserSelected);
        PrintInstances(state);

        while (true)
        {
            if (Logger.LogInput(Console.ReadKey(true)) is not string message)
                continue;
            try
            {
                (selectedChatOrUser, isUserSelected) = await Execute(state, client, selectedChatOrUser, isUserSelected, message);
            }
            catch (Exception e)
            {
                Logger.LogInputError(e.Message, e.GetType().ToString());
            }
        }
    }

    private static async Task<(Guid? selectedChatOrUser, bool isUserSelected)> Execute(PersistentClientState state, Client client, Guid? selectedChatOrUser, bool isUserSelected, string message)
    {
        string[] split = message.Split();
        string[] args = [.. split.Skip(1)];
        switch (split.FirstOrDefault(string.Empty).ToLowerInvariant())
        {
            case "@emit":
            {
                if (args.Length is not 1 || !Guid.TryParse(args[0], out Guid guid))
                    throw new ArgumentOutOfRangeException(nameof(message));
                InstanceInvite code = await client.RequestInviteCode(guid);
                PrintCommandOutput(code.ToString());
            }
            break;

            case "@map":
            {
                if (args.Length is not 0)
                    throw new ArgumentOutOfRangeException(nameof(message));
                throw new NotImplementedException();
            }
            //break;

            case "@redeem":
            {
                if (args.Length is not 1)
                    throw new ArgumentOutOfRangeException(nameof(message));
                InstanceInvite instanceInvite = InstanceInvite.FromString(args[0]);
                await client.AddInstance(instanceInvite);
                PrintInstances(state);
                PrintChats(state, selectedChatOrUser, isUserSelected);
            }
            break;

            case "@switch":
            {
                if (args.Length is not 1)
                    throw new ArgumentOutOfRangeException(nameof(message));

                if (!Guid.TryParse(args[0], out Guid guid))
                    throw new ArgumentOutOfRangeException(nameof(message));

                selectedChatOrUser = guid;
                bool userExists = state.Recipients.ContainsKey(guid);
                bool chatExists = state.ChatMembers.ContainsKey(guid);
                if (userExists ^ chatExists)
                    isUserSelected = userExists;
                else
                    throw new InvalidOperationException($"Error processing guid: '{nameof(userExists)}:{userExists}' nor '{nameof(chatExists)}:{chatExists}' matched an id or are identical");
            }
            break;

            //case "@make":
            //{

            //}
            //break;

            case "@help":
            {
                if (args.Length is not 0)
                    throw new ArgumentOutOfRangeException(nameof(message));
                PrintHelp();
            }
            break;

            case "@profile":
            {
                Logger.DisableInput();
                Logger.Reset();

                Profile? profile = await SetupProfile(state.Profile);
                if (profile is not null)
                {
                    // apply
                }
                Logger.EnableInput();
            }
            break;

            default:
            {
                if (selectedChatOrUser is not Guid guid)
                {
                    Logger.LogInputError("No one to send message to");
                    break;
                }

                Mail mail = new(state.Profile, new TextMailContent(message));
                RecordMailRequest request = new(mail, 0); // todo
                if (isUserSelected)
                    await client.Relay(request, state.Recipients[guid]);
                else
                    await client.Relay(request, guid);
            }
            break;
        }

        return (selectedChatOrUser, isUserSelected);
    }

    private static void PrintHelp()
        => Logger.LogNote(null,"""
                               @REDEEM <invite code>       redeem invite code and add the instance
                               @EMIT   <instance id>       request a new invite code from specified instance
                               @SWITCH <chat id|user id>   switch chats
                               @MAKE <[user id ..]>        create new chat
                               @HELP                       show this text
                               """);
                               //@PROFILE                    configure profile
                               //@MAP                        show map of connections
    private static void PrintChats(PersistentClientState state, Guid? selectedChatOrUser, bool isUserSelected)
    {
        if (state.Recipients.Count <= 0)
        {
            Logger.LogNote(null, "NO AVAILABLE CHATS");
            return;
        }
        else
            Logger.LogNote(null, "AVAILABLE CHATS:");
        if (state.ChatMembers.Count > 0)
            Logger.LogNote("GROUPS", FormatAsList(state.ChatMembers.Select(k => $"{k.Key} ({string.Join(", ", k.Value)})")));
        if (state.Recipients.Count > 0)
            Logger.LogNote("USERS", FormatAsList(state.Recipients.Keys));
        string message = selectedChatOrUser is null? "NO CHAT SELECTED" : $"NOW IN: {(isUserSelected? "(DM) " : string.Empty)}{selectedChatOrUser}";
        Logger.LogNote(null, message);
    }

    private static string FormatAsList<T>(IEnumerable<T> values) => string.Join('\n', values.Select(k => $"* {k}"));

    private static void PrintInstances(PersistentClientState state)
    {
        if (state.Instances.Count <= 0)
        {
            Logger.LogNote(null, "NO AVAILABLE INSTANCES");
            return;
        }
        Logger.LogNote("AVAILABLE INSTANCES", FormatAsList(state.Instances.Keys));
    }

    private static void PrintCommandOutput(string output)
    {
        Logger.LogNote("OUTPUT", output, ConsoleColor.Green);
    }
}