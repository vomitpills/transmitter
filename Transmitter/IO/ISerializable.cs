using Transmitter.Comms;
using Transmitter.Security;

namespace Transmitter.IO;

public interface ISerializable</*out*/ T> where T : ISerializable<T>
{
    public abstract void Serialize(BinaryWriter writer);
    public abstract static T Deserialize(BinaryReader reader);
}

public static class SerializableExtensions
{
    private static T FromBytes<T>(byte[] data, Func<BinaryReader, T> deserializer)
    {
        using MemoryStream stream = new(data);
        using BinaryReader reader = new(stream);
        return deserializer(reader);
    }

    private static ReadOnlyMemory<byte> ToBytes(Action<BinaryWriter> serializer)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        serializer(writer);
        return stream.ToArray();
    }

    extension<T>(ISerializable<T> serializable) where T : ISerializable<T>
    {
        public ReadOnlyMemory<byte> ToBytes() => ToBytes(serializable.Serialize);
        public static T FromBytes(byte[] data) => FromBytes(data, T.Deserialize);
    }

    extension(Message message)
    {
        public ReadOnlyMemory<byte> ToBytes() => ToBytes((writer) => message.Serialize(writer));
    }

    extension(HashedId passcode)
    {
        public ReadOnlyMemory<byte> ToBytes() => ToBytes(passcode.SerializeHash);
        public static HashedId FromBytes(byte[] data) => FromBytes(data, HashedId.DeserializeHash);
    }
}