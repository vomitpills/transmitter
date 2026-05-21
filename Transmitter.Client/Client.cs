using System.Net;

using Transmitter.Authentication;
using Transmitter.Comms;
using Transmitter.Comms.Gateways;
using Transmitter.Comms.Requests;
using Transmitter.Comms.Responses;

namespace Transmitter.Client;

public partial class Client : Transceiver
{
    private readonly PersistentClientState persistentState;

    readonly TcpGateway gateway = new(new(IPAddress.Any, 0));

    public Client(PersistentClientState state)
    {
        persistentState = state;
        Mount(gateway);
        gateway.Start();
    }

    public async Task<IChat> ConnectToInstance(IPAddress address, ushort port, ushort returnLinePort, Guid inviteCode)
    {
        QAConnection connection = await EstablishInstanceConnection(new(address, port), Role.Client);
        ClientSecret clientSecret = await ObtainClientSecret(connection, inviteCode);
        return await ConnectToInstance(connection, new(address, returnLinePort), clientSecret);
    }
    public async Task<IChat> ConnectToInstance(IPAddress address, ushort port, ushort returnLinePort, ClientSecret clientSecret)
    {
        QAConnection connection = await EstablishInstanceConnection(new(address, port), Role.Client);
        return await ConnectToInstance(connection, new(address, returnLinePort), clientSecret);
    }

    private async Task<Chat> ConnectToInstance(QAConnection connection, IPEndPoint returnLineEndpoint, ClientSecret clientSecret)
    {
        Guid returnLineKey = await GetReturnLineKey(connection, clientSecret);
        QAConnection returnLineConnection = await EstablishInstanceConnection(returnLineEndpoint, Role.Server);
        await AuthenticateCommConnection(connection, clientSecret);
        await AuthenticateReturnLine(returnLineConnection, clientSecret, returnLineKey);
        return new(connection, returnLineConnection, clientSecret);
    }

    private static async Task<ClientSecret> ObtainClientSecret(QAConnection connection, Guid inviteCode)
    {
        Response response = await connection.RequestAsync(new ObtainClientSecretRequest(inviteCode));
        return response switch
        {
            ClientSecretResponse clientSecretResponse => clientSecretResponse.ClientSecret,
            _ => throw new NotImplementedException()
        };
    }

    private static async Task AuthenticateCommConnection(QAConnection connection, ClientSecret clientSecret)
    {
        Response response = await connection.RequestAsync(new AuthenticateCommConnectionRequest(clientSecret));
        if (response.Status is not RequestStatus.OK)
            throw new NotImplementedException();
    }

    private static async Task AuthenticateReturnLine(QAConnection returnLineConnection, ClientSecret clientSecret, Guid returnLineKey)
    {
        Response response = await returnLineConnection.RequestAsync(new AuthenticateReturnLineConnectionRequest(clientSecret, returnLineKey));
        if (response.Status is not RequestStatus.OK)
            throw new NotImplementedException();
    }

    private static async Task<Guid> GetReturnLineKey(QAConnection connection, ClientSecret clientSecret)
    {
        Response response = await connection.RequestAsync(new ObtainReturnLineKeyRequest(clientSecret));
        if (response is not GuidResponse guidResponse)
            throw new NotImplementedException();
        return guidResponse.Guid;
    }

    private async Task<QAConnection> EstablishInstanceConnection(IPEndPoint endpoint, Role role) => await ConnectAsync(() => gateway.InitiateConnectionAsync(endpoint, new(role, 60)));
}

public interface IChat : IDisposable
{
    public event Action<Transaction>? MailReceived;
    public Task PostMail(Mail mail, Guid recipient);
}