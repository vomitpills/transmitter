namespace Transmitter.Comms;

public interface IDeserializationKey { }

public abstract class Message
{
    protected Message(IDeserializationKey key)
    {
        if (key is not MessageDeserializationKey)
            throw new InvalidOperationException("Invalid key");
    }

    protected Message() { }

    public void Serialize(BinaryWriter writer, Logger? logger = null)
    {
        SerializeDescendant(writer, logger);
    }

    protected static IDeserializationKey DeserializeRoot(BinaryReader reader)
    {
        return new MessageDeserializationKey();
    }

    protected abstract void SerializeDescendant(BinaryWriter writer, Logger? logger = null);

    private record class MessageDeserializationKey() : IDeserializationKey;
}