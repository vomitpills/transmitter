using System.Text.Json;
using System.Text.Json.Serialization;

namespace transmitter_alpha_common;

public class Mail(MailType mailType, string data)
{
    public MailType MailType { get; } = mailType;
    public string Data { get; } = data;
}

public enum MailType : byte
{
    Message, CacheUpdate
}

[JsonSerializable(typeof(Mail))]
[JsonSerializable(typeof(Profile))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class SourceGenerationContext : JsonSerializerContext { }

public static class Serializer
{
    public static JsonSerializerOptions JsonSerializerOptions { get; } = new() { TypeInfoResolver = SourceGenerationContext.Default };
}