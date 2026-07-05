using Transmitter.Comms.Responses;

namespace Transmitter.Comms.Requests;

public sealed class RelayMessageToServerRequest : AuthenticatedRequest<RelayMessageToServerRequest>, IAuthenticatedRequest
{
    static RequestIntention IAuthenticatedRequest.DescendantIntention { get; } = RequestIntention.RelayMessage;

    public Transaction Transaction { get; }

    public RelayMessageToServerRequest(ReadOnlyMemory<byte> clientSecret, Transaction transaction) : base(clientSecret)
    {
        Transaction = transaction;
    }

    private RelayMessageToServerRequest(IDeserializationKey key, RequestIntention intention, ReadOnlyMemory<byte> clientSecret, Transaction transaction) : base(key, intention, clientSecret)
    {
        Transaction = transaction;
    }

    protected override void FinalizeDescendantSerialization(BinaryWriter writer)
    {
        Transaction.Serialize(writer);
    }

    static Request IAuthenticatedRequest.FinalizeDescendantDeserialization(BinaryReader reader, RequestIntention intention, ReadOnlyMemory<byte> clientSecret, IDeserializationKey key)
    {
        Transaction transaction = Transaction.Deserialize(reader);
        return new RelayMessageToServerRequest(key, intention, clientSecret, transaction);
    }

    static ResponseDeserializationFinalizer IAuthenticatedRequest.FinalizeDescendantResponseDeserialization(ResponsePayloadType responsePayloadType) => responsePayloadType switch
    {
        ResponsePayloadType.Guid => GuidResponse.FinalizeDeserialization,
        _ => throw new ArgumentOutOfRangeException(nameof(responsePayloadType), responsePayloadType.ToString(), null)
    };
}

public sealed class RelayMessageToClientRequest : Request<RelayMessageToClientRequest>, IRequest
{
    public static RequestIntention Intention => RequestIntention.RelayMessage;

    public Transaction Transaction { get; }

    public RelayMessageToClientRequest(Transaction transaction)
    {
        Transaction = transaction;
    }

    private RelayMessageToClientRequest(IDeserializationKey key, RequestIntention intention, Transaction transaction) : base(key, intention)
    {
        Transaction = transaction;
    }

    public static Request FinalizeDeserialization(BinaryReader reader, RequestIntention intention, IDeserializationKey key)
    {
        Transaction transaction = Transaction.Deserialize(reader);
        return new RelayMessageToClientRequest(key, intention, transaction);
    }

    protected override void FinalizeSerialization(BinaryWriter writer)
    {
        Transaction.Serialize(writer);
    }

    static ResponseDeserializationFinalizer IRequest.FinalizeResponseDeserialization(ResponsePayloadType responsePayloadType) => responsePayloadType switch
    {
        ResponsePayloadType.Empty => EmptyResponse.FinalizeDeserialization,
        _ => throw new ArgumentOutOfRangeException(nameof(responsePayloadType), responsePayloadType.ToString(), null)
    };
}
