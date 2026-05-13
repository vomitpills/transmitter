using Transmitter.Authentication;

namespace Transmitter.Comms.Responses;

public sealed class ClientSecretResponse : Response<ClientSecretResponse>, IResponse
{
    public static ResponsePayloadType PayloadType { get; } = ResponsePayloadType.Guid;

    public override RequestStatus Status { get; }

    public ClientSecret ClientSecret { get; }

    public ClientSecretResponse(RequestStatus status, ClientSecret clientSecret)
    {
        Status = status;
        ClientSecret = clientSecret;
    }

    private ClientSecretResponse(IDeserializationKey key, RequestStatus status, ResponsePayloadType payloadType, ClientSecret clientSecret) : base(key, status, payloadType)
    {
        ClientSecret = clientSecret;
    }

    public static Response FinalizeDeserialization(BinaryReader reader, RequestStatus status, ResponsePayloadType payloadType, IDeserializationKey key)
    {
        return new ClientSecretResponse(key, status, payloadType, ClientSecret.Deserialize(reader));
    }

    protected override void SerializeFinal(BinaryWriter writer)
    {
        ClientSecret.Serialize(writer);
    }
}