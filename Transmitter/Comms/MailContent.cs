namespace Transmitter.Comms;

public interface IMailContent
{
    public abstract static MailContentType MailType { get; }
    public abstract static MailContent Deserialize(BinaryReader reader);
}

public abstract class MailContent
{
    public abstract void Serialize(BinaryWriter writer);
    public static MailContent Deserialize(BinaryReader reader, Func<MailContentType, MailContentDeserializer> deserializer)
    {
        MailContentType contentType = (MailContentType)reader.ReadByte();
        return deserializer(contentType)(reader);
    }
}

public delegate MailContent MailContentDeserializer(BinaryReader reader);

public abstract class MailContent<T> : MailContent where T : IMailContent
{
    public override void Serialize(BinaryWriter writer)
    {
        writer.Write((byte)T.MailType);
        SerializeDescendant(writer);
    }
    protected abstract void SerializeDescendant(BinaryWriter writer);
}

public enum MailContentType : byte
{
    Text, Media, Receipt,

    Custom = byte.MaxValue
}