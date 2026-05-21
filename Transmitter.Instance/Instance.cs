using System.Net;

using Transmitter.Authentication;
using Transmitter.Comms;
using Transmitter.Comms.Gateways;
using Transmitter.Comms.Requests;
using Transmitter.Comms.Responses;

namespace Transmitter.Instance;

public partial class Instance : Transceiver, IDisposable
{
    #if DEBUG
    private const string SAVE_FILE_NAME = "state-dev";
#else
    private const string SAVE_FILE_NAME = "state";
#endif

    private readonly PersistentServerState persistentState = PersistentServerState.LoadOrCreate(Path.Join(Environment.CurrentDirectory, SAVE_FILE_NAME));
    private readonly Dictionary<ReturnLineAccessCode, DateTimeOffset> returnLineAccessCodes = [];
    private readonly List<Guid> returnLineReachablePeers = [];

    private readonly Gateway gateway;
    private readonly Gateway returnLineGateway;

    private readonly Dictionary<Guid, (ClientSecret, DateTimeOffset)> returnLineKeys = [];

    private readonly List<Task> activeConnections = [];

    public Instance(ushort port, ushort returnLinePort)
    {
        gateway = new TcpGateway(new(IPAddress.Any, port));
        Mount(gateway);
        returnLineGateway = new TcpGateway(new(IPAddress.Any, returnLinePort));
        Mount(returnLineGateway);
    }

    public void Start()
    {
        gateway.Start();
        returnLineGateway.Start();

        NewConnection += DispatchNewConnection;
    }

    public void Stop()  
    {
        gateway.Stop();
        returnLineGateway.Stop();
    }

    public Guid EmitInviteCode() => persistentState.EmitInviteCode();

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
        Guid id = default;
        while (true)
        {
            authenticationGraceWindow.Token.ThrowIfCancellationRequested();

            using CancellationTokenSource requestGraceWindow = new(TimeSpan.FromSeconds(10));
            using CancellationTokenSource linkedToken = CancellationTokenSource.CreateLinkedTokenSource(authenticationGraceWindow.Token, requestGraceWindow.Token);

            Request request = await connectionAsServer.ReadRequestAsync((intention) => intention switch
            {
                RequestIntention.ObtainSecret => ObtainClientSecretRequest.FinalizeDeserialization,
                RequestIntention.ObtainReturnLineKey => ObtainReturnLineKeyRequest.FinalizeDeserialization,
                RequestIntention.Authenticate => AuthenticateCommConnectionRequest.FinalizeDeserialization,
                _ => throw new ArgumentOutOfRangeException(nameof(intention))
            }, linkedToken.Token);
                
            Response response = request switch
            {
                ObtainClientSecretRequest obtainClientSecretRequest
                    => persistentState.TryRegisterUser(obtainClientSecretRequest.InviteCode, out ClientSecret clientSecret)?
                    new ClientSecretResponse(RequestStatus.OK, clientSecret) : new EmptyResponse(RequestStatus.FAULTY_DATA),

                ObtainReturnLineKeyRequest obtainReturnLineKeyRequest
                    => new GuidResponse(RequestStatus.OK, EmitReturnLineKey(obtainReturnLineKeyRequest.ClientSecret)),

                AuthenticateCommConnectionRequest authRequest
                    => new EmptyResponse((authenticated = persistentState.TryGetId(authRequest.ClientSecret, out id))? RequestStatus.OK : RequestStatus.FAULTY_DATA),

                _ => throw new NotSupportedException()
            };
            await connectionAsServer.WriteResponseAsync(response, linkedToken.Token);

            if (authenticated)
                break;
        }

        Console.WriteLine(id);

        while (true)
        {
            Request request = await connectionAsServer.ReadRequestAsync((intention) => intention switch
            {
                RequestIntention.SendMail => SendMailRequest.FinalizeDeserialization,
                _ => throw new ArgumentOutOfRangeException(nameof(intention))
            });
            Response response = GetResponse(id, request);
            await connectionAsServer.WriteResponseAsync(response);
        }
    }

    private Response GetResponse(Guid id, Request request)
    {
        switch (request)
        {
            case SendMailRequest sendMailRequest:
            {
                Transaction transaction = new(id, sendMailRequest.Mail);
                bool success = persistentState.RecordPendingTransaction(transaction, sendMailRequest.Recipient);
                if (success)
                    return new GuidResponse(RequestStatus.OK, transaction.TransactionId);
                else
                    return new EmptyResponse(RequestStatus.FAULTY_DATA); // incorrect code
            }

            default:
                throw new NotSupportedException();
        }
    }

    private Guid EmitReturnLineKey(ClientSecret clientSecret)
    {
        Guid key = Guid.NewGuid();
        lock (returnLineKeys)
            returnLineKeys.Add(key, (clientSecret, DateTimeOffset.UtcNow.AddSeconds(30)));
        return key;
    }

    private async Task HandleConnectionAsClient(QAConnection connectionAsClient)
    {
        using CancellationTokenSource authenticationGraceWindow = new(TimeSpan.FromSeconds(20));
        bool authenticated = false;
        Guid id = default;
        while (true)
        {
            authenticationGraceWindow.Token.ThrowIfCancellationRequested();

            using CancellationTokenSource requestGraceWindow = new(TimeSpan.FromSeconds(10));
            using CancellationTokenSource linkedToken = CancellationTokenSource.CreateLinkedTokenSource(authenticationGraceWindow.Token, requestGraceWindow.Token);

            Request request = await connectionAsClient.ReadRequestAsync((intention) => intention switch
            {
                RequestIntention.Authenticate => AuthenticateReturnLineConnectionRequest.FinalizeDeserialization,
                _ => throw new ArgumentOutOfRangeException(nameof(intention))
            }, linkedToken.Token);

            if (request is not AuthenticateReturnLineConnectionRequest authRequest)
                throw new NotSupportedException();

            authenticated = persistentState.TryGetId(authRequest.ClientSecret, out id) && RedeemReturnLineKey(authRequest.ClientSecret, authRequest.ReturnLineKey);
            await connectionAsClient.WriteResponseAsync(new EmptyResponse(authenticated? RequestStatus.OK : RequestStatus.FAULTY_DATA), linkedToken.Token);

            if (authenticated)
                break;
        }

        while (true)
        {
            Transaction transaction = await persistentState.PeekTransactionQueue(id);
            Response response = await connectionAsClient.RequestAsync(new AcknowledgeMailRequest(transaction));
            if (!response.IndicatesSuccess)
                throw new NotImplementedException();
            persistentState.AdvanceTransactionQueue(id);
        }
    }

    private bool RedeemReturnLineKey(ClientSecret clientSecret, Guid key)
    {
        (ClientSecret Secret, DateTimeOffset Offset) retrievedValues;
        lock (returnLineKeys)
            if (!returnLineKeys.Remove(key, out retrievedValues))
                return false;
        if (clientSecret != retrievedValues.Secret || DateTimeOffset.UtcNow > retrievedValues.Offset)
            return false;
        return true;
    }

    private readonly record struct ReturnLineAccessCode(ClientSecret Auth, string Secret);

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        gateway.Dispose();
        returnLineGateway.Dispose();
        persistentState.Dispose();
    }
}