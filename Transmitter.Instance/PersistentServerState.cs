using Transmitter.Authentication;
using Transmitter.Comms;

namespace Transmitter.Instance;

internal sealed partial class PersistentServerState
{
    private HashSet<Guid> ValidInvites { get; init; } = [];
    private PeerCollection Peers { get; init; } = [];

    public Guid EmitInviteCode()
    {
        Guid inviteCode = Guid.NewGuid();
        ValidInvites.Add(inviteCode);
        Save();
        return inviteCode;
    }

    public bool TryRegisterUser(Guid inviteCode, out ClientSecret clientSecret)
    {
        if (ValidInvites.Remove(inviteCode))
        {
            Save();
            clientSecret = ClientSecret.Emit();
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
    public bool TryGetId(ClientSecret auth, out Guid guid)
    {
        if (!Peers.TryGet(auth, out Peer? peer))
        {
            guid = default;
            return false;
        }
        guid = peer.Id;
        return true;
    }

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

    public async Task<Transaction> PeekTransactionQueue(Guid receiver, CancellationToken cancellationToken = default)
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