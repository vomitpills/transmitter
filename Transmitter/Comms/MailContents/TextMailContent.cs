namespace Transmitter.Comms.MailContents;

public sealed class TextMailContent(string text) : MailContent<TextMailContent>, IMailContent
{
    public static MailContentType MailType { get; } = MailContentType.Text;

    public string Text { get; } = text;

    protected override void SerializeDescendant(BinaryWriter writer)
    {
        writer.Write(Text); // UTF-8 is automatically chosen by tcp gateway, but this is not enforced
    }
    public static MailContent Deserialize(BinaryReader reader)
    {
        string text = reader.ReadString();
        return new TextMailContent(text);
    }

    public override string ToString() => Text;
}