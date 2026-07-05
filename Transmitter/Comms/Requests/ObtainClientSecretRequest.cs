using Transmitter.Comms.Responses;

namespace Transmitter.Comms.Requests;

public sealed class ObtainClientSecretRequest : Request<ObtainClientSecretRequest>, IRequest
{
    public static RequestIntention Intention => RequestIntention.ObtainSecret;

    public Guid InviteCode { get; }
    public Profile Profile { get; }

    public ObtainClientSecretRequest(Guid inviteCode, Profile profile)
    {
        InviteCode = inviteCode;
        Profile = profile;
    }

    private ObtainClientSecretRequest(IDeserializationKey key, RequestIntention intention, Guid inviteCode, Profile profile) : base(key, intention)
    {
        InviteCode = inviteCode;
        Profile = profile;
    }

    public static Request FinalizeDeserialization(BinaryReader reader, RequestIntention intention, IDeserializationKey key)
    {
        Span<byte> guidBuffer = stackalloc byte[16];
        reader.ReadExactly(guidBuffer);
        Guid inviteCode = new(guidBuffer);
        Profile profile = Profile.Deserialize(reader);
        return new ObtainClientSecretRequest(key, intention, inviteCode, profile);
    }

    protected override void FinalizeSerialization(BinaryWriter writer)
    {
        writer.Write(InviteCode.ToByteArray());
        Profile.Serialize(writer);
    }

    static ResponseDeserializationFinalizer IRequest.FinalizeResponseDeserialization(ResponsePayloadType responsePayloadType) => responsePayloadType switch
    {
        ResponsePayloadType.AuthKeyAndReferrer => AuthKeyAndReferrerResponse.FinalizeDeserialization,
        ResponsePayloadType.Empty => EmptyResponse.FinalizeDeserialization,
        _ => throw new ArgumentOutOfRangeException(nameof(responsePayloadType), responsePayloadType.ToString(), null)
    };
}