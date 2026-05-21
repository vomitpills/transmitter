using Transmitter.Comms.Responses;

namespace Transmitter.Comms.Requests;

public sealed class KeepAliveRequest : Request<KeepAliveRequest>, IRequest
{
    public static RequestIntention Intention => RequestIntention.KeepAlive;

    public KeepAliveRequest() { }

    private KeepAliveRequest(IDeserializationKey key, RequestIntention intention) : base(key, intention) { }

    public static Request FinalizeDeserialization(BinaryReader reader, RequestIntention intention, IDeserializationKey key) => new KeepAliveRequest(key, intention);

    protected override void FinalizeSerialization(BinaryWriter writer) { }

    static ResponseDeserializationFinalizer IRequest.FinalizeResponseDeserialization(ResponsePayloadType responsePayloadType) => responsePayloadType switch
    {
        ResponsePayloadType.Empty => EmptyResponse.FinalizeDeserialization,
        _ => throw new ArgumentOutOfRangeException(nameof(responsePayloadType), responsePayloadType.ToString(), null)
    };
}