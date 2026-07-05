using Transmitter.Comms.Responses;

namespace Transmitter.Comms.Requests;

public sealed class TransactionCompletionNoticeRequest : Request<TransactionCompletionNoticeRequest>, IRequest
{
    public static RequestIntention Intention { get; } = RequestIntention.TransactionCompletionNotice;

    public Transaction Transaction { get; }
    public DateTimeOffset SentAt { get; }

    public TransactionCompletionNoticeRequest(Transaction transaction, DateTimeOffset sentAt)
    {
        Transaction = transaction;
        SentAt = sentAt;
    }

    private TransactionCompletionNoticeRequest(IDeserializationKey key, RequestIntention intention, Transaction transaction, DateTimeOffset sentAt) : base(key, intention)
    {
        Transaction = transaction;
        SentAt = sentAt;
    }

    protected override void FinalizeSerialization(BinaryWriter writer)
    {
        Transaction.Serialize(writer);
        writer.Write(SentAt.Ticks);
    }

    public static Request FinalizeDeserialization(BinaryReader reader, RequestIntention intention, IDeserializationKey key)
    {
        Transaction transaction = Transaction.Deserialize(reader);
        DateTimeOffset sentAt = new(reader.ReadInt64(), TimeSpan.Zero);
        return new TransactionCompletionNoticeRequest(key, intention, transaction, sentAt);
    }

    public static ResponseDeserializationFinalizer FinalizeResponseDeserialization(ResponsePayloadType responsePayloadType) => responsePayloadType switch
    {
        ResponsePayloadType.Empty => EmptyResponse.FinalizeDeserialization,
        _ => throw new ArgumentOutOfRangeException(nameof(responsePayloadType), responsePayloadType.ToString(), null)
    };
}
