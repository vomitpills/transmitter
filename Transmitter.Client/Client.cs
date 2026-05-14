using System.Net;

using Transmitter.Authentication;
using Transmitter.Comms;
using Transmitter.Comms.Gateways;
using Transmitter.Comms.Requests;
using Transmitter.Comms.Responses;

namespace Transmitter.Client;

public class Client : Transceiver
{
    public Profile Profile { get; private set; }
    private readonly Dictionary<Guid, Profile> profileCache = [];

    readonly TcpGateway gateway = new(new(IPAddress.Any, 0));

    public Client(Profile profile, IReadOnlyDictionary<Guid, Profile>? profileCache = null)
    {
        Profile = profile;
        if (profileCache is not null)
            this.profileCache = new(profileCache);
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
        await AuthenticateReturnLine(returnLineConnection, clientSecret, returnLineKey);
        return new(connection, returnLineConnection);
    }

    private static async Task<ClientSecret> ObtainClientSecret(QAConnection connection, Guid inviteCode)
    {
        Response response = await connection.RequestAsync(new ObtainClientSecretRequest(inviteCode), (payloadType) => payloadType switch
        {
            ResponsePayloadType.ClientSecret => ClientSecretResponse.FinalizeDeserialization, // make the request class implement this
            _ => throw new ArgumentOutOfRangeException(nameof(payloadType))
        });
        return response switch
        {
            ClientSecretResponse clientSecretResponse => clientSecretResponse.ClientSecret,
            _ => throw new NotImplementedException()
        };
    }

    private static async Task AuthenticateReturnLine(QAConnection returnLineConnection, ClientSecret clientSecret, Guid returnLineKey)
    {
        Response response = await returnLineConnection.RequestAsync(new AuthenticateReturnLineRequest(clientSecret, returnLineKey), (payloadType) => payloadType switch
        {
            ResponsePayloadType.Empty => EmptyResponse.FinalizeDeserialization,
            _ => throw new ArgumentOutOfRangeException(nameof(payloadType))
        });
        if (response.Status is not RequestStatus.OK)
            throw new NotImplementedException();
    }

    private static async Task<Guid> GetReturnLineKey(QAConnection connection, ClientSecret clientSecret)
    {
        Response response = await connection.RequestAsync(new ObtainReturnLineKeyRequest(clientSecret), (payloadType) => payloadType switch
        {
            ResponsePayloadType.Guid => GuidResponse.FinalizeDeserialization,
            ResponsePayloadType.Empty => EmptyResponse.FinalizeDeserialization,
            _ => throw new ArgumentOutOfRangeException(nameof(payloadType))
        });
        if (response is not GuidResponse guidResponse)
            throw new NotImplementedException();
        return guidResponse.Guid;
    }

    private class Chat(QAConnection connection, QAConnection returnLineConnection) : IChat
    {
        public event Func<Mail>? MailReceived;

        public Task PostMail(Mail mail)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            connection.Dispose();
            returnLineConnection.Dispose();
        }
    }

    private async Task<QAConnection> EstablishInstanceConnection(IPEndPoint endpoint, Role role) => await ConnectAsync(() => gateway.InitiateConnectionAsync(endpoint, new(role, 60)));
}

public interface IChat : IDisposable
{
    public event Func<Mail>? MailReceived;
    public Task PostMail(Mail mail);
}