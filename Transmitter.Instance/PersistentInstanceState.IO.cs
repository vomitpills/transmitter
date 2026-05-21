using System.Text;

using Transmitter.IO;

namespace Transmitter.Instance;

partial class PersistentInstanceState : IDisposable // move shared logic into an abstract class
{
    private const ushort FORMAT_VERSION = 0;

    private readonly AtomicWriter atomicWriter;

    private PersistentInstanceState(string filePath)
    {
        atomicWriter = new(filePath);
        Peers.ProfileUpdated += (_) => Save();
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
            writer.Write(inviteCode.ToByteArray());

        writer.Write((byte)Peers.Count);
        foreach (var peer in Peers)
            peer.Serialize(writer);
    }

    private static PersistentInstanceState Load(string filePath)
    {
        using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
        using BinaryReader reader = new(stream, Encoding.Default, true);

        if (reader.ReadUInt16() != FORMAT_VERSION)
            throw new InvalidOperationException("Format version mismatch");

        byte validInvitesLength = (byte)stream.ReadByte();
        Guid[] validInvites = new Guid[validInvitesLength];
        for (int i = 0; i < validInvitesLength; i++)
            validInvites[i] = new(reader.ReadBytes(16));

        byte peersCount = (byte)stream.ReadByte();
        Peer[] peers = new Peer[peersCount];
        for (int i = 0; i < peersCount; i++)
            peers[i] = Peer.Deserialize(reader);
        
        return new(filePath)
        {
            ValidInvites = [.. validInvites],
            Peers = [.. peers]
        };
    }

    public void Dispose()
    {
        atomicWriter.Dispose();
    }
}