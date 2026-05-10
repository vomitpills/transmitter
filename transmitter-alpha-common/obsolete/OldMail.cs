using System.Text.Json;
using System.Text.Json.Serialization;

namespace transmitter_alpha_common;

[Obsolete]
public class OldMail(MailType mailType, string data)
{
    public MailType MailType { get; } = mailType;
    public string Data { get; } = data;
}

[Obsolete]
public enum MailType : byte
{
    Message, CacheUpdate
}

[JsonSerializable(typeof(OldMail))]
[JsonSerializable(typeof(Profile))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[Obsolete]
internal partial class SourceGenerationContext : JsonSerializerContext { }

[Obsolete]
public static class OldSerializer
{
    public static JsonSerializerOptions JsonSerializerOptions { get; } = new() { TypeInfoResolver = SourceGenerationContext.Default };
}