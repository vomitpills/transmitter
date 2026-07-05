using Transmitter.Comms.Responses;

namespace Transmitter.Comms.Requests;

public sealed class ManageChatRequest : Request<ManageChatRequest>, IRequest
{
    public static RequestIntention Intention => RequestIntention.ManageChat;

    public ManageChatRequest(Mail mail, ulong messageId)
    {
        Mail = mail;
        MessageIdModulo = (byte)(messageId % byte.MaxValue);
    }

    private ManageChatRequest(IDeserializationKey key, RequestIntention intention, Mail mail, byte messageIdModulo) : base(key, intention)
    {
        Mail = mail;
        MessageIdModulo = messageIdModulo;
    }

    public static Request FinalizeDeserialization(BinaryReader reader, RequestIntention intention, IDeserializationKey key)
    {
        Mail mail = Mail.Deserialize(reader);
        byte messageIdModulo = reader.ReadByte();
        return new ManageChatRequest(key, intention, mail, messageIdModulo);
    }

    protected override void FinalizeSerialization(BinaryWriter writer)
    {
        Mail.Serialize(writer);
        writer.Write(MessageIdModulo);
    }

    static ResponseDeserializationFinalizer IRequest.FinalizeResponseDeserialization(ResponsePayloadType responsePayloadType) => responsePayloadType switch
    {
        ResponsePayloadType.Empty => EmptyResponse.FinalizeDeserialization,
        _ => throw new ArgumentOutOfRangeException(nameof(responsePayloadType), responsePayloadType.ToString(), null)
    };

    public enum Action : byte
    {
        Create, Add, Edit
    }
}
