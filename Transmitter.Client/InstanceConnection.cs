using System.Diagnostics.CodeAnalysis;

using Transmitter.Comms;
using Transmitter.Comms.Gateways;
using Transmitter.Comms.Requests;
using Transmitter.Comms.Responses;
using Transmitter.Security;

namespace Transmitter.Client;

public sealed class InstanceConnection
{
    public bool IsConnected => connection is not null && feedbackLineConnection is not null;
    public InstanceRegistration Registration { get; }

    private readonly ConnectAsync connectAsync;
    private QAConnection? connection;
    private QAConnection? feedbackLineConnection;
    private Task? feedbackLineTask;

    public event Action<TransactionReceipt>? TransactionRecieved;

    public delegate Task<QAConnection?> ConnectAsync(GatewayAddress address, ConnectionOptions options, CancellationToken cancellationToken = default);

    public static InstanceConnection Create(ConnectAsync connectAsync, InstanceRegistration registration)
        => new(connectAsync, null, null, registration);

    public static async Task<(InstanceConnection, InstanceRegistration, HashedId?)> Create(ConnectAsync connectAsync, InstanceInvite inviteCode, Profile profile)
    {
        QAConnection connection = await EstablishInstanceConnection(connectAsync, inviteCode.InstanceAddress.CommAddress, Role.Client);
        (ReadOnlyMemory<byte> auth, HashedId? referrer) = await connection.ObtainClientSecret(inviteCode.Code, profile);
        InstanceRegistration registration = new(inviteCode.InstanceAddress, auth);
        QAConnection feedbackLineConnection = await BootstrapFromComm(connectAsync, registration, connection);
        return (new(connectAsync, connection, feedbackLineConnection, registration), registration, referrer);
    }

    private InstanceConnection(ConnectAsync connectAsync, QAConnection? connection, QAConnection? feedbackLineConnection, InstanceRegistration registration)
    {
        this.connectAsync = connectAsync;
        this.connection = connection;
        this.feedbackLineConnection = feedbackLineConnection;
        if (feedbackLineConnection is not null)
            feedbackLineTask = StartFeedbackLineTask(feedbackLineConnection);
        Registration = registration;
    }

    private static async Task<QAConnection> BootstrapFromComm(ConnectAsync connectAsync, InstanceRegistration registration, QAConnection connection)
    {
        Guid feedbackLineKey = await connection.GetFeedbackLineKey(registration.Auth);
        QAConnection feedbackLineConnection = await EstablishInstanceConnection(connectAsync, registration.InstanceAddress.FeedbackLineAddress, Role.Server);

        await connection.AuthenticateCommConnection(registration.Auth);
        await feedbackLineConnection.AuthenticateFeedbackLine(registration.Auth, feedbackLineKey);

        return feedbackLineConnection;
    }

    public async Task Start()
    {
        if (IsConnected)
            throw new InvalidOperationException("Already started");

        connection = await EstablishInstanceConnection(Registration.InstanceAddress.CommAddress, Role.Client);
        feedbackLineConnection = await BootstrapFromComm(connectAsync, Registration, connection);
        feedbackLineTask = Task.Run(() => StartFeedbackLineTask(feedbackLineConnection));
    }

    public void Stop()
    {
        connection?.Dispose();
        feedbackLineConnection?.Dispose();

        connection = null;
        feedbackLineConnection = null;
        feedbackLineTask = null;

        // task should be killed by disposal of connections, but proper cleanup is still todo
    }

    private Task<QAConnection> EstablishInstanceConnection(GatewayAddress address, Role role) => EstablishInstanceConnection(connectAsync, address, role);
    private static async Task<QAConnection> EstablishInstanceConnection(ConnectAsync connect, GatewayAddress address, Role role) => await connect(address, new(role, (short)TimeSpan.FromMinutes(3).TotalSeconds)) ?? throw new NotImplementedException();

    public async Task<InstanceInvite> GetInviteCode()
    {
        EnsureConnection();
        Response response = await connection.RequestAsync(new ObtainReferrerCodeRequest(Registration.Auth));
        return response switch
        {
            GuidResponse guidResponse => new(Registration.InstanceAddress, guidResponse.Guid), // add validation referrer-side
            _ => throw new NotSupportedException(),
        };
    }

    public async Task Relay(Request request, HashedId recipient) // add expiry dates to transactions
    {
        EnsureConnection();
        Response response = await connection.RequestAsync(new RelayMessageToServerRequest(Registration.Auth, Transaction.FromSerializable(recipient, request)));
        switch (response)
        {
            case GuidResponse guidResponse:
                break;

            default:
                throw new NotSupportedException();
        }
    }

    public async Task<Guid> RequestInviteCode()
    {
        EnsureConnection();
        Response response = await connection.RequestAsync(new ObtainReferrerCodeRequest(Registration.Auth));
        return response switch
        {
            GuidResponse guidResponse => guidResponse.Guid,
            _ => throw new NotSupportedException(),
        };
    }

    [MemberNotNull(nameof(connection))]
    private void EnsureConnection()
    {
        if (connection is null)
            throw new NotImplementedException("Not started");
    }

    private async Task StartFeedbackLineTask(QAConnection feedbackLineConnection)
    {
        while (true)
        {
            Request request = await feedbackLineConnection.AcceptRequestAsync((intention) => intention switch
            {
                RequestIntention.TransactionCompletionNotice => TransactionCompletionNoticeRequest.FinalizeDeserialization,
                _ => throw new ArgumentOutOfRangeException(nameof(intention))
            });

            Response response = await ChooseResponse(request);
            await feedbackLineConnection.RespondAsync(response);
            switch (request)
            {
                case TransactionCompletionNoticeRequest transactionCompleteRequest:
                {
                    await feedbackLineConnection.RespondAsync(new EmptyResponse(RequestStatus.OK));
                    TransactionRecieved?.Invoke(new(transactionCompleteRequest.Transaction, transactionCompleteRequest.SentAt, DateTimeOffset.UtcNow));
                }
                break;

                default:
                    throw new InvalidOperationException();
            }
        }
    }

    private async Task<Response> ChooseResponse(Request request)
    {
        switch (request)
        {
            case TransactionCompletionNoticeRequest transactionCompleteRequest:
            {
                TransactionRecieved?.Invoke(new(transactionCompleteRequest.Transaction, transactionCompleteRequest.SentAt, DateTimeOffset.UtcNow));
                return new EmptyResponse(RequestStatus.OK);
            }

            default:
                return new EmptyResponse(RequestStatus.TEAPOT);
        }
    }

    public void Dispose()
    {
        connection?.Dispose();
        feedbackLineConnection?.Dispose();
    }
}