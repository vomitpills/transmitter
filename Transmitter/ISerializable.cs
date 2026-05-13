namespace Transmitter;

public interface ISerializable<T> where T : ISerializable<T>
{
    public abstract void Serialize(BinaryWriter writer);
    public abstract static T Deserialize(BinaryReader reader);
}

//public static class SerializableExtensions
//{
//    extension<T> (ISerializable<T> serializable) where T : ISerializable<T>
//    {
//        public byte[] GetBytes()
//        {
//            using MemoryStream stream = new();
//            serializable.Serialize(stream);
//            return stream.ToArray();
//        }
//        public string Encode() => Convert.ToBase64String(serializable.GetBytes());

//        public static T FromBytes(byte[] data)
//        {
//            using MemoryStream stream = new(data);
//            return T.Deserialize(stream);
//        }
//        public static T Decode(string code) => FromBytes<T>(Convert.FromBase64String(code));
//    }
//}