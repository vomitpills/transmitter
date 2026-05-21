using Transmitter.Comms.MailContents;

namespace Transmitter.Comms;

public class Mail(params IEnumerable<MailContent> content)
{
    // MailContent is class and thus can be mutated
    public IList<MailContent> Content { get; } = [.. content];

    public void Serialize(BinaryWriter writer)
    {
        writer.Write((byte)Content.Count);
        foreach (var item in Content)
            item.Serialize(writer);
    }

    public static Mail Deserialize(BinaryReader reader, Func<BinaryReader, MailContentDeserializer>? deserializerFallback = null)
    {
        MailContent[] content = new MailContent[reader.ReadByte()];
        for (int i = 0; i < content.Length; i++)
            content[i] = MailContent.Deserialize(reader, (contentType) => contentType switch
            {
                MailContentType.Text => TextMailContent.Deserialize,
                MailContentType.Custom => deserializerFallback?.Invoke(reader) ?? throw new ArgumentOutOfRangeException(nameof(contentType), "Unspecified custom content deserializer"),
                _ => throw new ArgumentOutOfRangeException(nameof(contentType))
            });
        return new(content);
    }
}