using System.Security.Cryptography;
using Transmitter;
using Transmitter.Authentication;

namespace Transmitter;

public partial class Server : IDisposable
{
    private static readonly TimeSpan AccessCodeExpirationTime = TimeSpan.FromMinutes(1);

    private readonly PersistentServerState persistentState = PersistentServerState.LoadOrCreate(Path.Join(Environment.CurrentDirectory, "state"));
    private readonly Dictionary<ReturnLineAccessCode, DateTimeOffset> returnLineAccessCodes = [];
    private readonly List<Guid> returnLineReachablePeers = [];

    bool isRunning = false;

    public void Start(int port, int returnLinePort)
    {
        if (isRunning)
            throw new InvalidOperationException("already running");
        isRunning = true;

        Task.Run(() => StartLoop(port));
        Task.Run(() => StartReturnLineLoop(returnLinePort));
    }

    public ClientSecret EmitInviteCode() => persistentState.EmitInviteCode();

    private Profile? CheckPeerState(OldMessage incomingRequest, bool checkProfile = true) => CheckPeerState(incomingRequest, out _, checkProfile);

    private Profile? CheckPeerState(OldMessage incomingRequest, out ClientSecret auth, bool checkProfile = true)
    {
        auth = CheckAuth(incomingRequest);
        if (!checkProfile)
            return null;
        return persistentState.GetProfile(auth) ?? throw new StateException("no-profile");
    }

    private ClientSecret CheckAuth(OldMessage incomingRequest)
    {
        ClientSecret auth = ClientSecret.Decode(incomingRequest["auth"]);
        if (!persistentState.UserExists(auth))
            throw new FaultyDataException("auth");
        return auth;
    }

    public string EmitReturnLineAccessCode(ClientSecret auth)
    {
        ReturnLineAccessCode accessCode = new(auth, RandomNumberGenerator.GetHexString(16));
        DateTimeOffset expiresOn = DateTimeOffset.UtcNow + AccessCodeExpirationTime;
        returnLineAccessCodes.Add(accessCode, expiresOn);
        _ = Task.Run(async () =>
        {
            await Task.Delay(AccessCodeExpirationTime);
            lock (returnLineAccessCodes)
                returnLineAccessCodes.Remove(accessCode);
        });
        return accessCode.Secret;
    }

    public void AuthenticateReturnLineConnection(ClientSecret auth, string accessCode)
    {
        ReturnLineAccessCode code = new(auth, accessCode);
        lock (returnLineAccessCodes)
        {
            if (!returnLineAccessCodes.TryGetValue(code, out DateTimeOffset expiresAt))
                throw new StateException("flow");
            returnLineAccessCodes.Remove(code);
            if (DateTime.UtcNow > expiresAt)
                throw new StateException("expired");
        }
    }

    private readonly record struct ReturnLineAccessCode(ClientSecret Auth, string Secret);

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        listener?.Dispose();
        returnLineListener?.Dispose();
        persistentState.Dispose();
    }
}