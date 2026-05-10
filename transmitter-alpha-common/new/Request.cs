using System.Collections.ObjectModel;

namespace transmitter_alpha_common;

public interface IRequest
{
    protected abstract static Request FinalizeDeserialization(BinaryReader stream, RequestIntention intention, IDeserializationKey key);
}

public enum RequestIntention : byte
{
    PeerCheck, // add challenge (e.g. sum two bytes from payload) // add cooldown for specific request intentions?
    CheckIn, CheckOut,

    GetId,

    MailStatusUpdate
}

public abstract class Request : Message
{
    public abstract ReadOnlyCollection<ResponseTemplate> ExpectedResponse { get; }
    public abstract RequestIntention Intention { get; }

    protected Request(IDeserializationKey key, RequestIntention intention) : base(key)
    {
        if (intention != Intention)
            throw new ArgumentOutOfRangeException(nameof(intention), "Intention mismatch");
    }

    protected Request() : base() { }

    public static Request BeginDeserialization(BinaryReader stream, Func<RequestIntention, Func<BinaryReader, RequestIntention, IDeserializationKey, Request>> deserializeFinishChooser)
    {
        using var _ = Logger.Shared.LogContext(nameof(BeginDeserialization));
        IDeserializationKey key = DeserializeRoot(stream);

        RequestIntention intention = (RequestIntention)stream.ReadByte();
        if (!Enum.IsDefined(intention))
            throw new ArgumentOutOfRangeException(nameof(stream), $"'{intention}' is not defined for {nameof(RequestIntention)}");

        return deserializeFinishChooser(intention)(stream, intention, key);
    }

    protected override void SerializeDescendant(BinaryWriter writer)
    {
        writer.Write((byte)Intention);
        SerializeFinal(writer);
    }

    protected abstract void SerializeFinal(BinaryWriter writer);
}