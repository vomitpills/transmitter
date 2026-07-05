using System.Security.Cryptography;

using Transmitter.Comms.MailContents;
using Transmitter.IO;

namespace Transmitter.Comms;

public class Mail : ISerializable<Mail>
{
    // MailContent is class and thus can be mutated
    public IList<MailContent> Content { get; }
    public readonly ReadOnlyMemory<byte> ProfileHash;

    public Mail(Profile profile, params IEnumerable<MailContent> content) : this(profile.Hash, content) { }

    private Mail(ReadOnlyMemory<byte> profileHash, params IEnumerable<MailContent> content)
    {
        ProfileHash = profileHash;
        Content = [.. content];
    }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(ProfileHash.Span);
        writer.Write((byte)Content.Count);
        foreach (var item in Content)
            item.Serialize(writer);
    }

    public static Mail Deserialize(BinaryReader reader)
    {
        byte[] profileHash = new byte[SHA256.HashSizeInBytes];
        reader.ReadExactly(profileHash);

        MailContent[] content = new MailContent[reader.ReadByte()];
        for (int i = 0; i < content.Length; i++)
            content[i] = MailContent.Deserialize(reader, (contentType) => contentType switch
            {
                MailContentType.Text => TextMailContent.Deserialize,
                _ => throw new ArgumentOutOfRangeException(nameof(contentType))
            });
        return new(profileHash, content);
    }


    // this exact pattern where serialization is simple and deserialization expects a deserializer also exists in Message etc. Maybe make an interface for that?
    // it's also different in a sense that the des. decider is optional. needs pondering.
    [Obsolete]
    public static Mail WIP_Deserialize(BinaryReader reader, Func<BinaryReader, MailContentDeserializer>? deserializerFallback = null)
    {
        byte[] profileHash = new byte[SHA256.HashSizeInBytes];
        reader.ReadExactly(profileHash);

        MailContent[] content = new MailContent[reader.ReadByte()];
        for (int i = 0; i < content.Length; i++)
            content[i] = MailContent.Deserialize(reader, (contentType) => contentType switch
            {
                MailContentType.Text => TextMailContent.Deserialize,
                MailContentType.Custom => deserializerFallback?.Invoke(reader) ?? throw new ArgumentOutOfRangeException(nameof(contentType), "Unspecified custom content deserializer"),
                _ => throw new ArgumentOutOfRangeException(nameof(contentType))
            });
        return new(profileHash, content);
    }
}