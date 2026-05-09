using System.Text;
using System.Text.Json;
using transmitter_alpha_common;

namespace transmitter_alpha_server;

internal sealed partial class PersistentServerState
{
    private HashSet<ClientSecret> ValidInvites { get; init; } = [];
    private PeerCollection Peers { get; init; } = [];

    public ClientSecret EmitInviteCode()
    {
        ClientSecret clientSecret = ClientSecret.EmitInviteCode();
        ValidInvites.Add(clientSecret);
        Save();
        return clientSecret;
    }

    public bool TryRegisterUser(ClientSecret inviteCode, out ClientSecret clientSecret)
    {
        if (inviteCode.IsAuthToken)
            throw new FaultyDataException("invite-code");

        if (ValidInvites.Remove(inviteCode))
        {
            Save();
            clientSecret = ClientSecret.EmitAuthToken();
            Peers.Add(new(clientSecret, null));
            Save();
            return true;
        }
        clientSecret = default;
        return false;
    }

    public Profile? GetProfile(Guid id) => Peers[id].Profile;
    public Profile? GetProfile(ClientSecret auth) => Peers[auth].Profile;

    public Guid GetId(ClientSecret auth) => Peers[auth].Id;

    public void UpdateProfile(ClientSecret auth, Profile profile)
    {
        Peers[auth].Profile = profile;
        Save();
    }

    public bool UserExists(Guid id) => Peers.Exists(id);
    public bool UserExists(ClientSecret auth) => Peers.Exists(auth);

    public bool RecordPendingTransaction(Transaction transaction, Guid receiver)
    {
        Peer peer = Peers[receiver];
        peer.MutateSemaphore.Wait();
        bool success = false;
        try
        {
            success = peer.Backlog.Writer.TryWrite(transaction);
        }
        finally
        {
            peer.MutateSemaphore.Release();
        }

        if (success)
            Save();

        return success;
    }

    public async Task<Transaction> BeginTransactionReadAsync(Guid receiver, CancellationToken cancellationToken = default)
    {
        Peer peer = Peers[receiver];
        Transaction? transaction;
        do
        {
            await peer.Backlog.Reader.WaitToReadAsync(cancellationToken);

        } while (!peer.Backlog.Reader.TryPeek(out transaction));
        return transaction;
    }

    public void AdvanceTransactionQueue(Guid receiver)
    {
        Peer peer = Peers[receiver];
        peer.MutateSemaphore.Wait();
        try
        {
            peer.Backlog.Reader.TryRead(out _);
        }
        finally
        {
            peer.MutateSemaphore.Release();
        }

        Save();
    }

    public IEnumerable<Guid> EnumerateKnownIds() => Peers.Select(p => p.Id);
}

public class Transaction
{
    public Mail Mail { get; }
    public Guid SenderId { get; }
    public Guid TransactionId { get; }

    public Transaction(Mail mail, Guid senderId) : this(mail, senderId, Guid.NewGuid()) { }

    private Transaction(Mail mail, Guid senderId, Guid transactionId)
    {
        Mail = mail;
        SenderId = senderId;
        TransactionId = transactionId;
    }

    public void Serialize(Stream stream)
    {
        stream.Write(SenderId.ToByteArray());
        stream.Write(TransactionId.ToByteArray());

        using BinaryWriter writer = new(stream, Encoding.Default, true);
        byte[] mail = JsonSerializer.SerializeToUtf8Bytes(Mail, Serializer.JsonSerializerOptions);
        writer.Write((ushort)mail.Length);
        stream.Write(mail);
    }

    public static Transaction Deserialize(Stream stream)
    {
        using BinaryReader reader = new(stream, Encoding.Default, true);
        
        Span<byte> guidBuffer = stackalloc byte[16];
        reader.ReadExactly(guidBuffer);
        Guid senderId = new(guidBuffer);
        reader.ReadExactly(guidBuffer);
        Guid transactionId = new(guidBuffer);

        ushort mailLength = reader.ReadUInt16();
        byte[] mailBuffer = new byte[mailLength];
        stream.ReadExactly(mailBuffer);
        Mail mail = JsonSerializer.Deserialize<Mail>(mailBuffer, Serializer.JsonSerializerOptions) ?? throw new InvalidOperationException();
        return new(mail, senderId, transactionId);
    }
}