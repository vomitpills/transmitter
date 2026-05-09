using System.Collections;
using System.Text;
using System.Threading.Channels;
using transmitter_alpha_common;

namespace transmitter_alpha_server;

internal sealed class Peer
{
    public ClientSecret Auth { get; }
    public Guid Id { get; }
    public Profile? Profile
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

    public Peer(ClientSecret auth, Profile? profile) : this(auth, Guid.NewGuid(), profile, []) { }

    private Peer(ClientSecret auth, Guid id, Profile? profile, IEnumerable<Transaction> backlog)
    {
        Auth = auth;
        Id = id;
        Profile = profile;
        Backlog = Channel.CreateUnbounded<Transaction>();
        foreach (var transaction in backlog)
            if (!Backlog.Writer.TryWrite(transaction))
                throw new NotImplementedException();
    }

    public void Serialize(Stream stream)
    {
        MutateSemaphore.Wait();
        try
        {
            Auth.Serialize(stream);
            stream.Write(Id.ToByteArray());
            if (Profile is not null)
            {
                stream.WriteByte(1);
                Profile.Serialize(stream);
            }
            else
                stream.WriteByte(0);

            using BinaryWriter writer = new(stream, Encoding.Default, true);
            ushort count = (ushort)Backlog.Reader.Count;
            writer.Write(count);
            Transaction[] buffer = new Transaction[count];
            for (int i = 0; i < count; i++)
            {
                if (!Backlog.Reader.TryRead(out Transaction? transaction))
                    throw new InvalidOperationException();
                buffer[i] = transaction;
                transaction.Serialize(stream);
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

    public static Peer Deserialize(Stream stream)
    {
        using BinaryReader reader = new(stream, Encoding.Default, true);
        ClientSecret auth = ClientSecret.Deserialize(stream);

        Span<byte> idBuffer = stackalloc byte[16];
        stream.ReadExactly(idBuffer);
        Guid id = new(idBuffer);

        bool hasProfile = stream.ReadByte() is 1;
        Profile? profile = hasProfile? Profile.Deserialize(stream) : null;

        ushort backlogCount = reader.ReadUInt16();
        Transaction[] backlog = new Transaction[backlogCount];
        for (int i = 0; i < backlogCount; i++)
            backlog[i] = Transaction.Deserialize(stream);

        return new(auth, id, profile, backlog);
    }

    public event Action<Profile?>? ProfileUpdated;
}

internal sealed class PeerCollection : IEnumerable<Peer>
{
    private readonly Dictionary<Guid, Peer> idToPeer = [];
    private readonly Dictionary<ClientSecret, Peer> authToPeer = [];

    public Peer this[Guid id] => idToPeer[id];
    public Peer this[ClientSecret auth] => authToPeer[auth];

    public int Count => idToPeer.Count;

    public event Action<Peer>? ProfileUpdated;
    public event Action<Peer>? PeerAdded;

    public void Add(Peer peer)
    {
        idToPeer[peer.Id] = peer;
        authToPeer[peer.Auth] = peer;

        peer.ProfileUpdated += (_) => ProfileUpdated?.Invoke(peer);
        PeerAdded?.Invoke(peer);
    }

    public bool Exists(Guid id) => idToPeer.ContainsKey(id);
    public bool Exists(ClientSecret auth) => authToPeer.ContainsKey(auth);

    public IEnumerator<Peer> GetEnumerator() => idToPeer.Values.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}