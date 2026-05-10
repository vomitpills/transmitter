namespace transmitter_alpha_common;

public interface IDeserializationKey { }

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
        using var _ = Logger.Shared.LogContext(nameof(Serialize), ToString());
        writer.Write(ProtocolMeta.ProtocolVersionSignature);
        SerializeDescendant(writer);
    }

    protected static IDeserializationKey DeserializeRoot(BinaryReader stream)
    {
        Span<byte> buffer = stackalloc byte[ProtocolMeta.ProtocolVersionSignatureLength];
        stream.ReadExactly(buffer);
        if (!buffer.SequenceEqual(ProtocolMeta.ProtocolVersionSignature))
            throw new ArgumentOutOfRangeException(nameof(stream), "Protocol version mismatch");
        return new MessageDeserializationKey();
    }

    protected abstract void SerializeDescendant(BinaryWriter writer);

    private class MessageDeserializationKey() : IDeserializationKey { }
}