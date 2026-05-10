using System.Collections.ObjectModel;

namespace Transmitter.Comms.Request;

public sealed class ObtainSecretRequest : Request, IRequest
{
    public override RequestIntention Intention => RequestIntention.ObtainSecret;
    public static ReadOnlyCollection<ResponseTemplate> ExpectedResponse { get; } = [new(ResponsePayloadType.ClientSecret)];

    public Guid InviteCode { get; }

    public ObtainSecretRequest(Guid inviteCode)
    {
        InviteCode = inviteCode;
    }

    private ObtainSecretRequest(IDeserializationKey key, RequestIntention intention, Guid inviteCode) : base(key, intention)
    {
        InviteCode = inviteCode;
    }

    public static Request FinalizeDeserialization(BinaryReader reader, RequestIntention intention, IDeserializationKey key)
    {
        Guid inviteCode = new(reader.ReadBytes(16));
        return new ObtainSecretRequest(key, intention, inviteCode);
    }

    protected override void FinalizeSerialization(BinaryWriter writer)
    {
        writer.Write(InviteCode.ToByteArray());
    }
}