using Transmitter.Comms.Responses;

namespace Transmitter.Comms.Requests;

public sealed class AcknowledgeMailRequest : Request<AcknowledgeMailRequest>, IRequest
{
    public static RequestIntention Intention { get; } = RequestIntention.AcknowledgeMailDelivery;

    public Transaction Transaction { get; }

    public AcknowledgeMailRequest(Transaction transaction)
    {
        Transaction = transaction;
    }

    private AcknowledgeMailRequest(IDeserializationKey key, RequestIntention intention, Transaction transaction) : base(key, intention)
    {
        Transaction = transaction;
    }

    protected override void FinalizeSerialization(BinaryWriter writer)
    {
        Transaction.Serialize(writer);
    }

    public static Request FinalizeDeserialization(BinaryReader reader, RequestIntention intention, IDeserializationKey key)
    {
        Transaction transaction = Transaction.Deserialize(reader);
        return new AcknowledgeMailRequest(key, intention, transaction);
    }

    public static ResponseDeserializationFinalizer FinalizeResponseDeserialization(ResponsePayloadType responsePayloadType) => responsePayloadType switch
    {
        ResponsePayloadType.Empty => EmptyResponse.FinalizeDeserialization,
        ResponsePayloadType.Guid => GuidResponse.FinalizeDeserialization,
        _ => throw new ArgumentOutOfRangeException(nameof(responsePayloadType), responsePayloadType.ToString(), null)
    };
}
