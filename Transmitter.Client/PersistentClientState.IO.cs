using System.Collections.ObjectModel;
using System.Text;

using Transmitter.Comms;
using Transmitter.IO;
using Transmitter.Security;

namespace Transmitter.Client;

partial class PersistentClientState : IDisposable // implement ISerializable and move Save and Load logic into a separate abstr.cl.
{
    private const ushort FORMAT_VERSION = 0;

    private readonly AtomicWriter atomicWriter;
    private readonly string filePath;

    private PersistentClientState(string filePath, Profile profile)
    {
        this.filePath = filePath;
        Profile = profile;

        atomicWriter = new(filePath);

        string dir = filePath + "@*.chat";
        foreach (string file in Directory.EnumerateFiles(Path.GetDirectoryName(dir) ?? throw new ArgumentOutOfRangeException(nameof(filePath)), Path.GetFileName(dir)))
        {
            string chatId = file[file.IndexOf('@')..file.IndexOf(".chat")];
            ChatMessageHistory[Guid.Parse(chatId)] = new(file);
        }
    }

    public static PersistentClientState FromPath(string filePath, Profile fallbackProfile)
    {
        PersistentClientState state;
        if (!File.Exists(filePath))
        {
            string directory = Path.GetDirectoryName(filePath)!;
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            state = new(filePath, fallbackProfile);
            state.Save();
        }
        else
            state = Load(filePath);
        return state;
    }

    private void Save()
    {
        using BinaryWriter writer = new(atomicWriter.BeginTransaction());

        writer.Write(FORMAT_VERSION);

        Profile.Serialize(writer);

        writer.Write(checked((ushort)Instances.Count));
        foreach (var item in Instances)
        {
            writer.Write(item.Key.ToByteArray());
            item.Value.Serialize(writer);
        }

        writer.Write(checked((ushort)Recipients.Count));
        foreach (var item in Recipients)
        {
            writer.Write(item.Key.ToByteArray());
            item.Value.Serialize(writer);
        }

        writer.Write(checked((ushort)ChatMembers.Count));
        foreach (var pair in ChatMembers)
        {
            writer.Write(pair.Key.ToByteArray());
            writer.Write(checked((byte)pair.Value.Count));
            foreach (var item in pair.Value)
                writer.Write(item.ToByteArray());
        }

        writer.Write(checked((ushort)PendingTransactions.Count));
        foreach (var item in PendingTransactions) // write a method Dictionary<TKey, TValue>
        {
            writer.Write(item.Key.ToByteArray());
            item.Value.Serialize(writer);
        }
    }

    private static PersistentClientState Load(string filePath)
    {
        #if DEBUG
        using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
#else
        using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
#endif
        using BinaryReader reader = new(stream, Encoding.Default, true);

        if (reader.ReadUInt16() != FORMAT_VERSION)
            throw new InvalidOperationException("Format version mismatch");

        Span<byte> guidSpan = stackalloc byte[16];
        ushort @ushort;

        Profile profile = Profile.Deserialize(reader);

        Dictionary<Guid, InstanceRegistration> instances = [];
        @ushort = reader.ReadUInt16();
        for (int _ = 0; _ < @ushort; _++)
        {
            reader.ReadExactly(guidSpan);
            Guid key = new(guidSpan);
            InstanceRegistration value = InstanceRegistration.Deserialize(reader);
            instances.Add(key, value);
        }

        Dictionary<Guid, Recipient> recipients = [];
        @ushort = reader.ReadUInt16();
        for (int _ = 0; _ < @ushort; _++)
        {
            reader.ReadExactly(guidSpan);
            Guid key = new(guidSpan);
            Recipient value = Recipient.Deserialize(reader);
            recipients.Add(key, value);
        }

        Dictionary<Guid, Collection<Guid>> members = [];
        @ushort = reader.ReadUInt16();
        for (int _ = 0; _ < @ushort; _++)
        {
            Collection<Guid> value = [];
            reader.ReadExactly(guidSpan);
            Guid key = new(guidSpan);
            for (int m = 0; m < reader.ReadByte(); m++)
            {
                reader.ReadExactly(guidSpan);
                value.Add(new(guidSpan));
            }
            members.Add(key, value);
        }

        Dictionary<Guid, PendingTransaction> pendingTransactions = [];
        @ushort = reader.ReadUInt16();
        for (int _ = 0; _ < @ushort; _++)
        {
            reader.ReadExactly(guidSpan);
            Guid key = new(guidSpan);
            PendingTransaction value = PendingTransaction.Deserialize(reader);
            pendingTransactions.Add(key, value);
        }

        return new(filePath, profile)
        {
            Instances = instances,
            Recipients = recipients,
            ChatMembers = members,
            PendingTransactions = pendingTransactions,
        };
    }

    private string GetChatFilename(Guid chatId) => $"{filePath}@{chatId}.chat";

    public void Dispose()
    {
        atomicWriter.Dispose();
    }

    public void AddRecipient(InstanceAddress address, HashedId recipientContact) // temporary. should associate with existing recipient if exists. add message to server response
    //public void AddRecipient(ReadOnlySpan<byte> message, RecipientContact contact)
    {
        Recipients.Add(Guid.NewGuid(), new([new(address, recipientContact)]));
        Save();
    }
}
