namespace Transmitter.Comms.Response;

public sealed class EmptyResponse : Response, IResponse
{
    public override ResponsePayloadType PayloadType => ResponsePayloadType.Empty;

    public override RequestStatus Status { get; }

    public EmptyResponse(RequestStatus status)
    {
        Status = status;
    }

    private EmptyResponse(IDeserializationKey key, RequestStatus status, ResponsePayloadType payloadType) : base(key, status, payloadType) { }

    public static Response FinalizeDeserialization(BinaryReader stream, RequestStatus status, ResponsePayloadType payloadType, IDeserializationKey key) => new EmptyResponse(key, status, payloadType);

    protected override void SerializeFinal(BinaryWriter writer) { }
}