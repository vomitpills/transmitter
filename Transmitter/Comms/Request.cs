namespace Transmitter.Comms;

public interface IRequest
{
    public abstract static ResponseDeserializationFinalizer FinalizeResponseDeserialization(ResponsePayloadType responsePayloadType);
    public abstract static Request FinalizeDeserialization(BinaryReader stream, RequestIntention intention, IDeserializationKey key);
    public static abstract RequestIntention Intention { get; }
}

// add cooldown for specific request intentions?
public enum RequestIntention : byte
{
    KeepAlive,
    ObtainSecret,
    ObtainFeedbackLineKey,
    ObtainReferrerKey,
    Authenticate,
    RelayMessage,
    RecordMail,
    TransactionCompletionNotice,
    ManageChat,
}

public abstract class Request : Message // this is atrocious
{
    protected Request(IDeserializationKey key) : base(key) { }

    protected Request() { }

    public static Request BeginDeserialization(BinaryReader stream, Func<RequestIntention, RequestDeserializationFinalizer> finalizeFactory, ConnectionLogger? logger = null)
    {
        IDeserializationKey key = DeserializeRoot(stream);

        RequestIntention intention = (RequestIntention)stream.ReadByte();
        if (!Enum.IsDefined(intention))
            throw new ArgumentOutOfRangeException(nameof(stream), $"'{intention}' is not defined for {nameof(RequestIntention)}");

        logger?.Log($"Reading request ({nameof(intention)}:{intention})");
        return finalizeFactory(intention)(stream, intention, key);
    }
}

public abstract class Request<T> : Request where T : IRequest
{
    protected Request(IDeserializationKey key, RequestIntention intention) : base(key)
    {
        if (intention != T.Intention)
            throw new ArgumentOutOfRangeException(nameof(intention), "Intention mismatch");
    }

    protected Request() : base() { }

    protected sealed override void SerializeDescendant(BinaryWriter writer, ConnectionLogger? logger = null)
    {
        logger?.Log($"Writing request ({nameof(T.Intention)}:{T.Intention})");
        writer.Write((byte)T.Intention);
        FinalizeSerialization(writer);
    }
    protected abstract void FinalizeSerialization(BinaryWriter writer);
}

public delegate Request RequestDeserializationFinalizer(BinaryReader reader, RequestIntention intention, IDeserializationKey key);