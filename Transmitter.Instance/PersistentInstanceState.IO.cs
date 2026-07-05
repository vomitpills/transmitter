using System.Text;

using Transmitter.IO;
using Transmitter.Security;

namespace Transmitter.Instance;

partial class PersistentInstanceState : IDisposable
{
    private const ushort FORMAT_VERSION = 0;

    private readonly AtomicWriter atomicWriter;

    private PersistentInstanceState(string filePath)
    {
        atomicWriter = new(filePath);
        Peers.ProfileUpdated += (_, _) => Save();
        Peers.PeerAdded += (_) => Save();
    }

    public static PersistentInstanceState LoadOrCreate(string filePath)
    {
        PersistentInstanceState state;
        if (!File.Exists(filePath))
        {
            state = new(filePath);
            state.Save();
        }
        else
            state = Load(filePath);
        return state;
    }

    private void Save()
    {
        using BinaryWriter writer = new(atomicWriter.BeginTransaction());
        writer.Write(BitConverter.GetBytes(FORMAT_VERSION));
        
        writer.Write((byte)ValidInvites.Count);
        foreach (var inviteCode in ValidInvites)
        {
            writer.Write(inviteCode.Key.ToByteArray());
            if (inviteCode.Value is HashedId referer)
            {
                writer.Write(true);
                referer.SerializeHash(writer);
            }
            else
                writer.Write(false);
        }

        Peers.Serialize(writer);
        writer.Flush();
    }

    private static PersistentInstanceState Load(string filePath)
    {
        using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
        using BinaryReader reader = new(stream, Encoding.Default, true);

        if (reader.ReadUInt16() != FORMAT_VERSION)
            throw new InvalidOperationException("Format version mismatch");

        byte validInvitesLength = (byte)stream.ReadByte();
        Dictionary<Guid, HashedId?> validInvites = [];
        Span<byte> guidBuffer = stackalloc byte[16]; // scary!
        for (int i = 0; i < validInvitesLength; i++)
        {
            reader.ReadExactly(guidBuffer);
            Guid guid = new(guidBuffer);
            HashedId? referer = reader.ReadBoolean()? HashedId.DeserializeHash(reader) : null;
            validInvites[guid] = referer;
        }

        PeerCollection peers = PeerCollection.Deserialize(reader);

        return new(filePath)
        {
            ValidInvites = validInvites,
            Peers = peers
        };
    }

    public void Dispose()
    {
        atomicWriter.Dispose();
    }
}