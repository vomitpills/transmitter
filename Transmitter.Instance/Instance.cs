using System.Net;

using Transmitter.Comms;
using Transmitter.Comms.Gateways;
using Transmitter.Comms.Requests;
using Transmitter.Comms.Responses;
using Transmitter.Security;

namespace Transmitter.Instance;

public partial class Instance : Transceiver, IDisposable
{
    private readonly PersistentInstanceState persistentState;

    private readonly Dictionary<HashedId, (Guid, DateTimeOffset)> feedbackLineKeys = []; // change tuple to custom type
    private readonly List<Guid> feedbackLineReachablePeers = [];

    private readonly List<Task> activeConnections = [];

    public Instance(ushort port, ushort feedbackLinePort, PersistentInstanceState state)
    {
        persistentState = state;
        Mount(new TcpGateway(new(IPAddress.Any, port), new("INSTANCE-GATEWAY")));
        Mount(new TcpGateway(new(IPAddress.Any, feedbackLinePort), new("INSTANCE-GATEWAY-FB")));
        NewConnection += DispatchNewConnection;
    }

    public void Start()
    {
        foreach (var item in gatewayRack)
            item.Start();
    }

    public void Stop()  
    {
        foreach (var item in gatewayRack)
            item.Stop();
    }

    public bool TryEmitInviteCode(ReadOnlySpan<byte> auth, out Guid inviteCode) => persistentState.TryEmitInviteCode(auth, out inviteCode);
    public Guid EmitUnboundInviteCode() => persistentState.EmitUnboundInviteCode();

    private void DispatchNewConnection(QAConnection connection)
    {
        Func<Task> task = connection.ConnectionOptions.Role switch
        {
            Role.Server => () => HandleConnectionAsServer(connection),
            Role.Client => () => HandleConnectionAsClient(connection),
            _ => throw new ArgumentOutOfRangeException(nameof(connection))
        };
        activeConnections.Add(Task.Run(task));
    }

    private async Task HandleConnectionAsServer(QAConnection connectionAsServer)
    {
        using CancellationTokenSource authenticationGraceWindow = new(TimeSpan.FromSeconds(25));
        bool authenticated = false;
        HashedId peer = default;
        while (true)
        {
            authenticationGraceWindow.Token.ThrowIfCancellationRequested();

            using CancellationTokenSource requestGraceWindow = new(TimeSpan.FromSeconds(10));
            using CancellationTokenSource linkedToken = CancellationTokenSource.CreateLinkedTokenSource(authenticationGraceWindow.Token, requestGraceWindow.Token);

            Request request = await connectionAsServer.AcceptRequestAsync((intention) => intention switch
            {
                RequestIntention.ObtainSecret => ObtainClientSecretRequest.FinalizeDeserialization,
                RequestIntention.ObtainFeedbackLineKey => ObtainFeedbackLineKeyRequest.FinalizeDeserialization,
                RequestIntention.Authenticate => AuthenticateCommConnectionRequest.FinalizeDeserialization,
                _ => throw new ArgumentOutOfRangeException(nameof(intention))
            }, linkedToken.Token);
                
            Response response = request switch
            {
                ObtainClientSecretRequest obtainClientSecretRequest
                    => persistentState.TryRegisterUser(obtainClientSecretRequest.InviteCode, obtainClientSecretRequest.Profile, out ReadOnlySpan<byte> clientKey, out HashedId? referrer)?
                        new AuthKeyAndReferrerResponse(RequestStatus.OK, clientKey.ToArray(), referrer) : new EmptyResponse(RequestStatus.FAULTY_DATA),

                ObtainFeedbackLineKeyRequest obtainFeedbackLineKeyRequest
                    => TryEmitFeedbackLineKey(obtainFeedbackLineKeyRequest.ClientAuth, out Guid key)?
                        new GuidResponse(RequestStatus.OK, key) : new EmptyResponse(RequestStatus.FAULTY_DATA),

                AuthenticateCommConnectionRequest authRequest
                    => new EmptyResponse((authenticated = persistentState.Authenticate(authRequest.ClientAuth.Span, out peer))? RequestStatus.OK : RequestStatus.FAULTY_DATA),

                _ => new EmptyResponse(RequestStatus.AUTH_FALT)
            };
            await connectionAsServer.RespondAsync(response, linkedToken.Token);

            if (authenticated)
                break;
        }

        while (true)
        {
            Request request = await connectionAsServer.AcceptRequestAsync((intention) => intention switch
            {
                RequestIntention.RelayMessage => RelayMessageToServerRequest.FinalizeDeserialization,
                RequestIntention.ObtainReferrerKey => ObtainReferrerCodeRequest.FinalizeDeserialization,
                _ => throw new ArgumentOutOfRangeException(nameof(intention))
            });
            Response response = await ChooseResponse(peer, request);
            await connectionAsServer.RespondAsync(response);
        }
    }

    private async Task<Response> ChooseResponse(HashedId sender, Request request)
    {
        switch (request)
        {
            case RelayMessageToServerRequest relayMessageRequest:
            {
                Transaction transaction = relayMessageRequest.Transaction;
                await persistentState.RecordPendingTransaction(transaction.SwapUser(sender), transaction.SenderOrRecipient);
                return new GuidResponse(RequestStatus.OK, transaction.Id);
            }

            case ObtainReferrerCodeRequest obtainReferrerCodeRequest:
            {
                return TryEmitInviteCode(obtainReferrerCodeRequest.ClientAuth.Span, out Guid inviteCode)
                    ? new GuidResponse(RequestStatus.OK, inviteCode)
                    : new EmptyResponse(RequestStatus.FAULTY_DATA);
            }

            default:
                return new EmptyResponse(RequestStatus.TEAPOT);
        }
    }

    private bool TryEmitFeedbackLineKey(ReadOnlyMemory<byte> clientAuth, out Guid key)
    {
        if (!persistentState.Authenticate(clientAuth.Span, out HashedId peer))
        {
            key = default;
            return false;
        }

        key = Guid.NewGuid();
        lock (feedbackLineKeys)
            feedbackLineKeys.Add(peer, (key, DateTimeOffset.UtcNow.AddSeconds(30)));
        return true;
    }

    private async Task HandleConnectionAsClient(QAConnection connectionAsClient)
    {
        using CancellationTokenSource authenticationGraceWindow = new(TimeSpan.FromSeconds(20));
        bool authenticated = false;
        HashedId peer = default;
        while (true)
        {
            authenticationGraceWindow.Token.ThrowIfCancellationRequested();

            using CancellationTokenSource requestGraceWindow = new(TimeSpan.FromSeconds(10));
            using CancellationTokenSource linkedToken = CancellationTokenSource.CreateLinkedTokenSource(authenticationGraceWindow.Token, requestGraceWindow.Token);

            Request request = await connectionAsClient.AcceptRequestAsync((intention) => intention switch
            {
                RequestIntention.Authenticate => AuthenticateFeedbackLineConnectionRequest.FinalizeDeserialization,
                _ => throw new ArgumentOutOfRangeException(nameof(intention))
            }, linkedToken.Token);

            if (request is not AuthenticateFeedbackLineConnectionRequest authRequest)
                throw new NotSupportedException();

            authenticated = persistentState.Authenticate(authRequest.ClientAuth.Span, out peer) && RedeemFeedbackLineKey(authRequest.ClientAuth.Span, authRequest.FeedbackLineKey);
            await connectionAsClient.RespondAsync(new EmptyResponse(authenticated? RequestStatus.OK : RequestStatus.FAULTY_DATA), linkedToken.Token);

            if (authenticated)
                break;
        }

        while (true)
        {
            Transaction transaction = await persistentState.PeekTransactionQueue(peer);
            Response response = await connectionAsClient.RequestAsync(new RelayMessageToClientRequest(transaction));
            if (!response.IndicatesSuccess)
                throw new NotImplementedException(); // wait and retry, drop connection after n attempts
            persistentState.AdvanceTransactionQueue(peer);
        }
    }

    private bool RedeemFeedbackLineKey(ReadOnlySpan<byte> auth, Guid key)
    {
        if (!persistentState.Authenticate(auth, out HashedId peer))
            return false;

        (Guid Key, DateTimeOffset Timeout) retrievedValues;
        lock (feedbackLineKeys)
            if (!feedbackLineKeys.Remove(peer, out retrievedValues))
                return false;
        return retrievedValues.Key == key && DateTimeOffset.UtcNow <= retrievedValues.Timeout;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        foreach (var item in gatewayRack)
            item.Dispose();
        persistentState.Dispose();
    }
}