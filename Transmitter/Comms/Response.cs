namespace Transmitter.Comms;

public interface IResponse
{
    protected abstract static Response FinalizeDeserialization(BinaryReader stream, RequestStatus status, ResponsePayloadType payloadType, IDeserializationKey key);
}

public enum RequestStatus : sbyte
{
    OK = 0,
    CACHED = 30,

    MALFORMED_REQUEST = -20,
    PROTOCOL_VERSION_MISMATCH = -40
}

public enum ResponsePayloadType : byte
{
    Empty,
    Guid,
    ClientSecret
}

public abstract class Response : Message
{
    public abstract RequestStatus Status { get; }
    public abstract ResponsePayloadType PayloadType { get; }

    protected Response(IDeserializationKey key, RequestStatus status, ResponsePayloadType payloadType) : base(key)
    {
        if (status != Status)
            throw new ArgumentOutOfRangeException(nameof(status), "Status mismatch");
        if (payloadType != PayloadType)
            throw new ArgumentOutOfRangeException(nameof(payloadType), "Payload type mismatch");
    }

    protected Response() : base() { }

    public static Response BeginDeserialization(BinaryReader stream, Func<ResponsePayloadType, Func<BinaryReader, RequestStatus, ResponsePayloadType, IDeserializationKey, Response>> deserializeFinishChooser)
    {
        IDeserializationKey key = DeserializeRoot(stream);

        RequestStatus status = (RequestStatus)unchecked((sbyte)stream.ReadByte());
        if (!Enum.IsDefined(status))
            throw new ArgumentOutOfRangeException(nameof(stream), $"'{status}' is not defined for {nameof(RequestStatus)}");

        ResponsePayloadType payloadType = (ResponsePayloadType)stream.ReadByte();
        if (!Enum.IsDefined(payloadType))
            throw new ArgumentOutOfRangeException(nameof(stream), $"'{payloadType}' is not defined for {nameof(ResponsePayloadType)}");

        return deserializeFinishChooser(payloadType)(stream, status, payloadType, key);
    }

    protected override void SerializeDescendant(BinaryWriter writer)
    {
        writer.Write(unchecked((byte)Status));
        writer.Write((byte)PayloadType);
        SerializeFinal(writer);
    }

    protected abstract void SerializeFinal(BinaryWriter writer);
}