using Transmitter.Security;

namespace Transmitter.Comms.Responses;

public sealed class AuthKeyAndReferrerResponse : Response<AuthKeyAndReferrerResponse>, IResponse
{
    public static ResponsePayloadType PayloadType { get; } = ResponsePayloadType.AuthKeyAndReferrer;

    public override RequestStatus Status { get; }

    public ReadOnlyMemory<byte> AuthKey { get; }
    public HashedId? Referrer { get; }

    public AuthKeyAndReferrerResponse(RequestStatus status, ReadOnlyMemory<byte> securityPasscodeKey, HashedId? referrer) // mayhaps should use init
    {
        Status = status;
        AuthKey = securityPasscodeKey;
        Referrer = referrer;
    }

    private AuthKeyAndReferrerResponse(IDeserializationKey key, RequestStatus status, ResponsePayloadType payloadType, ReadOnlyMemory<byte> securityPasscodeKey, HashedId? referrer) : base(key, status, payloadType)
    {
        AuthKey = securityPasscodeKey;
        Referrer = referrer;
    }

    public static Response FinalizeDeserialization(BinaryReader reader, RequestStatus status, ResponsePayloadType payloadType, IDeserializationKey key)
    {
        ReadOnlyMemory<byte> authKey = HashedId.DeserializeKey(reader);

        HashedId? referrer = reader.ReadBoolean()? HashedId.DeserializeHash(reader) : null;
        return new AuthKeyAndReferrerResponse(key, status, payloadType, authKey, referrer);
    }

    protected override void SerializeFinal(BinaryWriter writer) // whole setup feels kinda extra; maybe just make a generic Response<TPayload> class (?)
    {
        HashedId.SerializeKey(writer, AuthKey.Span);
        if (Referrer.HasValue)
        {
            writer.Write(true);
            Referrer.Value.SerializeHash(writer);
        }
        else
            writer.Write(false);
    }
}