namespace Transmitter.Comms;

public interface IDeserializationKey { }

// this is a rudimentary class kept in case we need to insert some data before the common request/response serialization scheme. might get deleted later.
public abstract class Message
{
    protected Message(IDeserializationKey key)
    {
        if (key is not MessageDeserializationKey)
            throw new InvalidOperationException("Invalid key");
    }

    protected Message() { }

    public void Serialize(BinaryWriter writer)
    {
        SerializeDescendant(writer);
    }

    protected static IDeserializationKey DeserializeRoot(BinaryReader stream)
    {
        return new MessageDeserializationKey();
    }

    protected abstract void SerializeDescendant(BinaryWriter writer);

    private class MessageDeserializationKey() : IDeserializationKey { }
}