using Transmitter.Comms;
using Transmitter.Security;

namespace Transmitter.Instance;

public sealed partial class PersistentInstanceState
{
    private Dictionary<Guid, HashedId?> ValidInvites { get; init; } = [];
    private PeerCollection Peers { get; init; } = [];

    public bool TryEmitInviteCode(ReadOnlySpan<byte> auth, out Guid inviteCode)
    {
        if (!Authenticate(auth, out HashedId peer))
        {
            inviteCode = default;
            return false;
        }
        inviteCode = Guid.NewGuid();
        ValidInvites.Add(inviteCode, peer);
        Save();
        return true;
    }

    public Guid EmitUnboundInviteCode()
    {
        Guid inviteCode = Guid.NewGuid();
        ValidInvites.Add(inviteCode, null);
        Save();
        return inviteCode;
    }

    public bool TryRegisterUser(Guid inviteCode, Profile profile, out ReadOnlySpan<byte> auth, out HashedId? referrer)
    {
        if (!ValidInvites.Remove(inviteCode, out referrer))
        {
            auth = default;
            return false;
        }
        auth = HashedId.Emit(out HashedId passcode);
        Peers.Add(new(passcode, profile));
        //RecordPendingTransaction();
        Save();
        return true;
    }

    public Profile GetProfile(HashedId passcode) => Peers[passcode].Profile;

    public bool Authenticate(ReadOnlySpan<byte> auth, out HashedId peer)
    {
        peer = HashedId.FromKey(auth);
        return UserExists(peer);
    }

    public void UpdateProfile(HashedId auth, Profile profile)
    {
        Peers[auth].Profile = profile;
        Save();
    }

    public bool UserExists(HashedId auth) => Peers.Exists(auth);

    public async Task RecordPendingTransaction(Transaction transaction, HashedId recipient)
    {
        Peer recipientPeer = Peers[recipient];
        recipientPeer.MutateSemaphore.Wait();
        try
        {
            await recipientPeer.Backlog.Writer.WriteAsync(transaction);
        }
        finally
        {
            recipientPeer.MutateSemaphore.Release();
        }
        Save();
    }

    public async Task<Transaction> PeekTransactionQueue(HashedId receiver, CancellationToken cancellationToken = default)
    {
        Peer peer = Peers[receiver];
        Transaction transaction;
        do
        {
            await peer.Backlog.Reader.WaitToReadAsync(cancellationToken);

        } while (!peer.Backlog.Reader.TryPeek(out transaction));
        return transaction;
    }

    public void AdvanceTransactionQueue(HashedId receiver)
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

    //public IEnumerable<Guid> EnumerateKnownIds() => Peers.Select(p => p.Id);
}