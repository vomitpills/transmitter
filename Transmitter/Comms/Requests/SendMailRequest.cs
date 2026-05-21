using Transmitter.Authentication;
using Transmitter.Authentication;
using Transmitter.Comms.Responses;

namespace Transmitter.Comms.Requests;

public sealed class SendMailRequest : AuthenticatedRequest<SendMailRequest>, IAuthenticatedRequest
{
    static RequestIntention IAuthenticatedRequest.DescendantIntention { get; } = RequestIntention.SendMail;

    public Guid Recipient { get; }
    public Mail Mail { get; }

    public SendMailRequest(ClientSecret clientSecret, Guid recipient, Mail mail) : base(clientSecret)
    {
        Recipient = recipient;
        Mail = mail;
    }

    private SendMailRequest(IDeserializationKey key, RequestIntention intention, ClientSecret clientSecret, Guid recipient, Mail mail) : base(key, intention, clientSecret)
    {
        Recipient = recipient;
        Mail = mail;
    }

    protected override void FinalizeDescendantSerialization(BinaryWriter writer)
    {
        writer.Write(Recipient.ToByteArray());
        Mail.Serialize(writer);
    }

    static Request IAuthenticatedRequest.FinalizeDescendantDeserialization(BinaryReader reader, RequestIntention intention, ClientSecret clientSecret, IDeserializationKey key)
    {
        Guid recipient = new(reader.ReadBytes(16));
        Mail mail = Mail.Deserialize(reader); // allow custom content types
        return new SendMailRequest(key, intention, clientSecret, recipient, mail);
    }

    static ResponseDeserializationFinalizer IAuthenticatedRequest.FinalizeDescendantResponseDeserialization(ResponsePayloadType responsePayloadType) => responsePayloadType switch
    {
        ResponsePayloadType.Empty => EmptyResponse.FinalizeDeserialization,
        ResponsePayloadType.Guid => GuidResponse.FinalizeDeserialization,
        _ => throw new ArgumentOutOfRangeException(nameof(responsePayloadType), responsePayloadType.ToString(), null)
    };
}
