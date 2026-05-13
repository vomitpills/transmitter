namespace Transmitter.Comms.Responses;

public sealed class GuidResponse : Response<GuidResponse>, IResponse
{
    public static ResponsePayloadType PayloadType { get; } = ResponsePayloadType.Guid;

    public override RequestStatus Status { get; }

    public Guid Guid { get; }

    public GuidResponse(RequestStatus status, Guid guid)
    {
        Status = status;
        Guid = guid;
    }

    private GuidResponse(IDeserializationKey key, RequestStatus status, ResponsePayloadType payloadType, Guid guid) : base(key, status, payloadType)
    {
        Guid = guid;
    }

    public static Response FinalizeDeserialization(BinaryReader stream, RequestStatus status, ResponsePayloadType payloadType, IDeserializationKey key)
    {
        Span<byte> buffer = stackalloc byte[16];
        stream.ReadExactly(buffer);
        Guid guid = new(buffer);
        return new GuidResponse(key, status, payloadType, guid);
    }

    protected override void SerializeFinal(BinaryWriter writer)
    {
        writer.Write(Guid.ToByteArray());
    }
}