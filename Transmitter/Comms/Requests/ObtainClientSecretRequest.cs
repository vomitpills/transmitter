using Transmitter.Comms.Responses;

namespace Transmitter.Comms.Requests;

public sealed class ObtainClientSecretRequest : Request<ObtainClientSecretRequest>, IRequest
{
    public static RequestIntention Intention => RequestIntention.ObtainSecret;

    public Guid InviteCode { get; }

    public ObtainClientSecretRequest(Guid inviteCode)
    {
        InviteCode = inviteCode;
    }

    private ObtainClientSecretRequest(IDeserializationKey key, RequestIntention intention, Guid inviteCode) : base(key, intention)
    {
        InviteCode = inviteCode;
    }

    public static Request FinalizeDeserialization(BinaryReader reader, RequestIntention intention, IDeserializationKey key)
    {
        Span<byte> guidBuffer = stackalloc byte[16];
        reader.ReadExactly(guidBuffer);
        Guid inviteCode = new(guidBuffer);
        return new ObtainClientSecretRequest(key, intention, inviteCode);
    }

    protected override void FinalizeSerialization(BinaryWriter writer)
    {
        writer.Write(InviteCode.ToByteArray());
    }

    static ResponseDeserializationFinalizer IRequest.FinalizeResponseDeserialization(ResponsePayloadType responsePayloadType) => responsePayloadType switch
    {
        ResponsePayloadType.ClientSecret => ClientSecretResponse.FinalizeDeserialization,
        ResponsePayloadType.Empty => EmptyResponse.FinalizeDeserialization,
        _ => throw new ArgumentOutOfRangeException(nameof(responsePayloadType))
    };
}