using System.Net.NetworkInformation;

namespace Transmitter.Comms;

public interface IResponse
{
    public abstract static Response FinalizeDeserialization(BinaryReader stream, RequestStatus status, ResponsePayloadType payloadType, IDeserializationKey key);
    public abstract static ResponsePayloadType PayloadType { get; }
}

public enum RequestStatus : sbyte
{
    OK = 0,
    CACHED = 30,

    PROTOCOL_VERSION_MISMATCH = -40,
    MALFORMED_REQUEST = -20,
    TEAPOT = -25
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

    protected Response(IDeserializationKey key) : base(key) { }

    protected Response() : base() { }

    public static Response BeginDeserialization(BinaryReader reader, Func<ResponsePayloadType, ResponseDeserializationFinalizer> deserializeFinishChooser)
    {
        IDeserializationKey key = DeserializeRoot(reader);

        RequestStatus status = (RequestStatus)unchecked((sbyte)reader.ReadByte());
        if (!Enum.IsDefined(status))
            throw new ArgumentOutOfRangeException(nameof(reader), $"'{status}' is not defined for {nameof(RequestStatus)}");

        ResponsePayloadType payloadType = (ResponsePayloadType)reader.ReadByte();
        if (!Enum.IsDefined(payloadType))
            throw new ArgumentOutOfRangeException(nameof(reader), $"'{payloadType}' is not defined for {nameof(ResponsePayloadType)}");

        return deserializeFinishChooser(payloadType)(reader, status, payloadType, key);
    }
}

public abstract class Response<T> : Response where T : Response<T>, IResponse
{
    protected Response(IDeserializationKey key, RequestStatus status, ResponsePayloadType payloadType) : base(key)
    {
        if (status != Status)
            throw new ArgumentOutOfRangeException(nameof(status), "Status mismatch");
        if (payloadType != T.PayloadType)
            throw new ArgumentOutOfRangeException(nameof(payloadType), "Payload type mismatch");
    }

    protected Response() : base() { }

    protected sealed override void SerializeDescendant(BinaryWriter writer)
    {
        writer.Write(unchecked((byte)Status));
        writer.Write((byte)T.PayloadType);
        SerializeFinal(writer);
    }
    protected abstract void SerializeFinal(BinaryWriter writer);
}

public delegate Response ResponseDeserializationFinalizer(BinaryReader reader, RequestStatus status, ResponsePayloadType payloadType, IDeserializationKey key);