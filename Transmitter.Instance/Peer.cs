using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

using Transmitter.Comms;
using Transmitter.IO;
using Transmitter.Security;

namespace Transmitter.Instance;

internal sealed class Peer : ISerializable<Peer>
{
    public HashedId Passcode { get; }
    public Profile Profile
    {
        get;
        set
        {
            MutateSemaphore.Wait();
            try
            {
                field = value;
                ProfileUpdated?.Invoke(value);
            }
            finally
            {
                MutateSemaphore.Release();
            }
        }
    }
    public Channel<Transaction> Backlog { get; }
    public SemaphoreSlim MutateSemaphore { get; } = new(1);

    public Peer(HashedId auth, Profile profile) : this(auth, profile, []) { }

    private Peer(HashedId passcode, Profile profile, IEnumerable<Transaction> backlog)
    {
        Passcode = passcode;
        Profile = profile;
        Backlog = Channel.CreateUnbounded<Transaction>();
        foreach (var transaction in backlog)
            if (!Backlog.Writer.TryWrite(transaction))
                throw new NotImplementedException();
    }

    public void Serialize(BinaryWriter writer)
    {
        MutateSemaphore.Wait();
        try
        {
            Passcode.SerializeHash(writer);
            Profile.Serialize(writer);

            ushort count = (ushort)Backlog.Reader.Count;
            writer.Write(count);
            Transaction[] buffer = new Transaction[count];
            for (int i = 0; i < count; i++)
            {
                if (!Backlog.Reader.TryRead(out Transaction transaction))
                    throw new InvalidOperationException();
                buffer[i] = transaction;
                transaction.Serialize(writer);
            }
            foreach (var item in buffer)
                if (!Backlog.Writer.TryWrite(item))
                    throw new InvalidOperationException();
        }
        finally
        {
            MutateSemaphore.Release();
        }
    }

    public static Peer Deserialize(BinaryReader reader)
    {
        HashedId passcode = HashedId.DeserializeHash(reader);

        Profile profile = Profile.Deserialize(reader);

        ushort backlogCount = reader.ReadUInt16();
        Transaction[] backlog = new Transaction[backlogCount];
        for (int i = 0; i < backlogCount; i++)
            backlog[i] = Transaction.Deserialize(reader);

        return new(passcode, profile, backlog);
    }

    public event Action<Profile>? ProfileUpdated;
}

internal sealed class PeerCollection : IEnumerable<Peer>, ISerializable<PeerCollection>
{
    private readonly List<Peer> peers = [];

    public PeerCollection() { }

    private PeerCollection(IEnumerable<Peer> peers)
    {
        this.peers = [.. peers];
    }

    private Peer? TryGetPeer(HashedId passcode) => peers.SingleOrDefault(p => p.Passcode == passcode);

    public Peer this[HashedId passcode] => TryGetPeer(passcode) ?? throw new KeyNotFoundException();

    public bool TryGet(HashedId peerId, [NotNullWhen(true)] out Peer? peer)
    {
        if (TryGetPeer(peerId) is Peer retrieved)
        {
            peer = retrieved;
            return true;
        }
        peer = null;
        return false;
    }

    public bool TryGet(ReadOnlySpan<byte> authKey, [NotNullWhen(true)] out Peer? peer) => TryGet(HashedId.FromKey(authKey), out peer);

    public int Count => peers.Count;

    public event Action<Peer, Profile>? ProfileUpdated;
    public event Action<Peer>? PeerAdded;

    public void Add(Peer peer)
    {
        peers.Add(peer);

        peer.ProfileUpdated += (profile) => ProfileUpdated?.Invoke(peer, profile);
        PeerAdded?.Invoke(peer);
    }

    public bool Exists(HashedId peerId) => peers.Exists(p => p.Passcode == peerId);
    public bool Exists(ReadOnlySpan<byte> auth) => Exists(HashedId.FromKey(auth));

    public void Serialize(BinaryWriter writer)
    {
        writer.Write((byte)Count);
        foreach (var peer in peers)
            peer.Serialize(writer);
    }

    public static PeerCollection Deserialize(BinaryReader reader)
    {
        byte peersCount = reader.ReadByte();
        Peer[] peers = new Peer[peersCount];
        for (int i = 0; i < peersCount; i++)
            peers[i] = Peer.Deserialize(reader);
        return [.. peers];
    }

    public IEnumerator<Peer> GetEnumerator() => peers.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}