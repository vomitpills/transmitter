using System.Diagnostics.CodeAnalysis;
using System.Text;
using Transmitter.Common;
using transmitter_common.cli;
using transmitter_common.cli.Screens;

namespace Transmitter.Client;

internal static class Program
{
#if DEBUG
    private const string CONFIG_NAME = "config-02.debug";
#else
    private const string CONFIG_NAME = "config-02";
#endif
    private static string ConfigFileLocation { get; } = AppConfig.GetFileLocation(CONFIG_NAME);

    private const int RECONNECT_ATTEMPTS = 5;
    private static readonly TimeSpan reconnectInterval = TimeSpan.FromSeconds(5);

    public static async Task Main()
    {
        Console.OutputEncoding = Encoding.Unicode;
        if (!AppConfig.TryLoad(CONFIG_NAME, out AppConfig? appConfig))
            appConfig = await SetupAppConfig();

        Client? client = null;
        while (true)
        {
            try
            {
                client = new(appConfig.ClientBootstrapInfo, appConfig.Profile);
                client.BootstrapInfoUpdated += (bootstrapInfo) => appConfig.UpdateBootstrapInfo(bootstrapInfo);
                await client.Start();
                break;
            }
            catch (Exception e)
            {
                client?.Dispose();
                Logger.LogNote("Failed to connect to server", e.Message, ConsoleColor.Red);
                Logger.LogNote(null, "Press enter to retry.", ConsoleColor.White);
                Console.ReadLine();
            }
        }

        Logger.EnableInput();

        Task healthCheckTask = client.AwaitAllTasks();

        while (true)
        {
            try
            {
                if (healthCheckTask.IsFaulted)
                    throw healthCheckTask.Exception;

                if (Logger.LogInput(Console.ReadKey(true)) is not string message)
                    continue;

                switch (message)
                {
                    case "!setprofile":
                        Logger.DisableInput();
                        await client.Pause();
                        Logger.Reset();

                        Profile? profile = await SetupProfile(client.Profile);
                        await client.Unpause();
                        healthCheckTask = client.AwaitAllTasks();
                        if (profile is not null)
                        {
                            await client.UpdateProfile(profile);
                            appConfig.UpdateProfile(profile);
                        }

                        Logger.EnableInput();
                        break;

                    case "!togglesound":
                        Logger.LogNote(null, $"Switched sound to {(Logger.ToggleSound()? "ON" : "OFF")} (will reset on app restart)", ConsoleColor.Cyan);
                        break;

                    default:
                        OldMail mail = new(MailType.Message, message);
                        await client.SendMail(mail);
                        Logger.LogMessage(client.Profile, message, false);
                        break;
                }
            }
            catch (Exception e)
            {
                Logger.DisableInput();
                Logger recoveryLogger = new("recovery", ConsoleColor.Red);
                recoveryLogger.Write(string.Empty);
                recoveryLogger.LogProgress("Lost connection; previous action aborted");
                int i = RECONNECT_ATTEMPTS;
                for (; i > 0; i--)
                {
                    recoveryLogger.LogProgress($"Attempting to reconnect ({i}/{RECONNECT_ATTEMPTS})");
                    await client.Stop();
                    try
                    {
                        await client.Start();
                        healthCheckTask = client.AwaitAllTasks();
                        break;
                    }
                    catch
                    {
                        if (i is 0)
                            continue;
                        recoveryLogger.LogProgress($"Failed to reconnect. Sleeping {reconnectInterval.TotalSeconds}s before next attempt");
                        await Task.Delay(reconnectInterval);
                    }
                }
                if (i is 0)
                {
                    recoveryLogger.Write(string.Empty);
                    recoveryLogger.LogProgress("Recconection attempts exhausted, exiting");
                    recoveryLogger.Write(string.Empty);
                    Logger.LogNote("Cause of crash", e.ToString(), ConsoleColor.Red);
                    Logger.LogNote(null, "You can now close the program", ConsoleColor.Red);
                    await Task.Delay(Timeout.Infinite);
                }
                else
                {
                    recoveryLogger.LogProgress("Reconnected");
                    Logger.EnableInput();
                }
            }
        }
    }

    private static async Task<AppConfig> SetupAppConfig()
    {
        ClientBootstrapInfo clientBootstrapInfo;
        Profile? profile;
        while (true)
        {
            Console.WriteLine("no config found, setting up a new one.");
            Console.WriteLine("enter bootstrap code:");
            Console.Write("╰→ ");
            string code = Console.ReadLine() ?? throw new InvalidOperationException();

            try
            {
                clientBootstrapInfo = ClientBootstrapInfo.Decode(code);
                if (clientBootstrapInfo.ClientSecret.IsAuthToken)
                    throw new InvalidOperationException("Can't bootstrap with an auth token");
            }
            catch (Exception e)
            {
                Console.WriteLine("error:");
                Console.WriteLine(e);
                continue;
            }

            ConsoleIO.Initialize();

            ConfirmConfigCreationScreen confirmScreen = new(clientBootstrapInfo.ServerAddress, ConfigFileLocation);
            confirmScreen.EnableInput();
            do
            {
                ConsoleIO.ShowScreen(confirmScreen);
                await confirmScreen.WaitForInvalidationAsync();
            } while (confirmScreen.Status is null);
            confirmScreen.DisableInput();

            ConsoleIO.Reset();

            if (confirmScreen.Status is true)
            {
                if ((profile = await SetupProfile()) is not null)
                    break;
            }
        }
        return AppConfig.CreateNew(CONFIG_NAME, clientBootstrapInfo, profile);
    }

    private static async Task<Profile?> SetupProfile(Profile? oldProfile = null)
    {
        ConsoleIO.Initialize();

        ProfileCustomizeScreen profileCustomizeScreen = new(oldProfile);
        profileCustomizeScreen.EnableInput();
        do
        {
            ConsoleIO.ShowScreen(profileCustomizeScreen);
            await profileCustomizeScreen.WaitForInvalidationAsync();
        } while (profileCustomizeScreen.Status is null);
        profileCustomizeScreen.DisableInput();

        ConsoleIO.Reset();

        return profileCustomizeScreen.Profile!;
    }
}

internal class AppConfig : IDisposable
{
    private static string ConfigFileDirectory { get; } = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "transmitter");
    private const ushort CONFIG_FORMAT_VERSION = 0;

    public ClientBootstrapInfo ClientBootstrapInfo { get; private set; }
    public Profile Profile { get; private set; }
    private readonly Stream storageStream;

    private AppConfig(Stream storageStream, ClientBootstrapInfo clientBootstrapInfo, Profile profile)
    {
        this.storageStream = storageStream;
        ClientBootstrapInfo = clientBootstrapInfo;
        Profile = profile;
    }

    public void UpdateProfile(Profile profile)
    {
        Profile = profile;
        Save();
    }

    public void UpdateBootstrapInfo(ClientBootstrapInfo bootstrapInfo)
    {
        ClientBootstrapInfo = bootstrapInfo;
        Save();
    }

    public static string GetFileLocation(string name) => Path.Join(ConfigFileDirectory, $"{name}.bin"); // sanitize filename or use lookup

    public static bool TryLoad(string name, [NotNullWhen(true)] out AppConfig? config)
    {
        string location = GetFileLocation(name);
        if (!File.Exists(location))
        {
            config = null;
            return false;
        }

        FileStream? stream = null;
        try
        {
            stream = new(location, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            using BinaryReader reader = new(stream, Encoding.Default, true);
            if (reader.ReadUInt16() != CONFIG_FORMAT_VERSION)
                throw new InvalidOperationException("Config format version mismatch");
            ClientBootstrapInfo clientBootstrapInfo = ClientBootstrapInfo.Deserialize(stream);
            Profile profile = Profile.Deserialize(stream);
            config = new(stream, clientBootstrapInfo, profile);
            return true;
        }
        catch
        {
            stream?.Dispose();
            throw;
        }
    }

    public static AppConfig CreateNew(string configName, ClientBootstrapInfo clientBootstrapInfo, Profile profile)
    {
        FileStream? stream = null;
        try
        {
            Directory.CreateDirectory(ConfigFileDirectory);
            stream = new(GetFileLocation(configName), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            AppConfig config = new(stream, clientBootstrapInfo, profile);
            config.Save();
            return config;
        }
        catch
        {
            stream?.Dispose();
            throw;
        }
    }

    public void Save() // create backup before writing
    {
        storageStream.SetLength(0);
        storageStream.Write(BitConverter.GetBytes(CONFIG_FORMAT_VERSION));
        ClientBootstrapInfo.Serialize(storageStream);
        Profile.Serialize(storageStream);
        storageStream.Flush();
    }

    public void Dispose()
    {
        storageStream.Dispose();
    }
}