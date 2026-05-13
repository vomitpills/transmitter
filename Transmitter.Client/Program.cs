using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Transmitter.Client;

internal static class Program
{
#if DEBUG
    private const string CONFIG_NAME = "config-03.debug";
#else
    private const string CONFIG_NAME = "config-03";
#endif
    private static string ConfigFileLocation { get; } = AppConfig.GetFileLocation(CONFIG_NAME);

    private const int RECONNECT_ATTEMPTS = 5;
    private static readonly TimeSpan reconnectInterval = TimeSpan.FromSeconds(5);

    public static async Task Main()
    {
        throw new NotImplementedException();
    }
}

[Obsolete]
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
            ClientBootstrapInfo clientBootstrapInfo = default;
            Profile profile = Profile.Deserialize(reader);
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
        using BinaryWriter writer = new(storageStream, Encoding.Default, true);
        storageStream.SetLength(0);
        storageStream.Write(BitConverter.GetBytes(CONFIG_FORMAT_VERSION));
        Profile.Serialize(writer);
        storageStream.Flush();
    }

    public void Dispose()
    {
        storageStream.Dispose();
    }
}