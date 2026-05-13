using System.Collections.ObjectModel;

namespace Transmitter.Comms;

public interface IRequest
{
    public abstract static ReadOnlyCollection<ResponseTemplate> ExpectedResponse { get; }
    public abstract static Request FinalizeDeserialization(BinaryReader stream, RequestIntention intention, IDeserializationKey key);
    public static abstract RequestIntention Intention { get; }
}

// add challenge (e.g. sum two bytes from payload)
// add cooldown for specific request intentions?
public enum RequestIntention : byte
{
    ObtainSecret,
    Authenticate
}

public abstract class Request : Message // this is atrocious
{
    protected Request(IDeserializationKey key) : base(key) { }

    protected Request() { }

    public static Request BeginDeserialization(BinaryReader stream, Func<RequestIntention, RequestDeserializationFinalizer> finalizeFactory)
    {
        using var _ = Logger.Shared.LogContext(nameof(BeginDeserialization));
        IDeserializationKey key = DeserializeRoot(stream);

        RequestIntention intention = (RequestIntention)stream.ReadByte();
        if (!Enum.IsDefined(intention))
            throw new ArgumentOutOfRangeException(nameof(stream), $"'{intention}' is not defined for {nameof(RequestIntention)}");

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

    protected sealed override void SerializeDescendant(BinaryWriter writer)
    {
        writer.Write((byte)T.Intention);
        FinalizeSerialization(writer);
    }
    protected abstract void FinalizeSerialization(BinaryWriter writer);
}

public delegate Request RequestDeserializationFinalizer(BinaryReader reader, RequestIntention intention, IDeserializationKey key);