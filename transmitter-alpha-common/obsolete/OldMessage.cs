using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace transmitter_alpha_common;

[Obsolete]
public class OldMessage : IDictionary<string, string>
{
    public OldMessage()
    {

    }

    public OldMessage(Dictionary<string, string> values)
    {
        this.values = values;
    }

    private readonly Dictionary<string, string> values = [];

    public ICollection<string> Keys => ((IDictionary<string, string>)values).Keys;

    public ICollection<string> Values => ((IDictionary<string, string>)values).Values;

    public int Count => ((ICollection<KeyValuePair<string, string>>)values).Count;

    public bool IsReadOnly => ((ICollection<KeyValuePair<string, string>>)values).IsReadOnly;

    public string this[string key] { get => ((IDictionary<string, string>)values)[key]; set => ((IDictionary<string, string>)values)[key] = value; }

    public static readonly JsonSerializerOptions VisualSerializerOptions = new(OldSerializer.JsonSerializerOptions) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public async Task Serialize(Stream stream, Logger? logger)
    {
        using Logger.ContextTracker? contextTracker = logger?.LogContext(nameof(Serialize));
        string json = JsonSerializer.Serialize(values, OldSerializer.JsonSerializerOptions);
        byte[] data = Encoding.UTF8.GetBytes(json);

        contextTracker?.Write(JsonSerializer.Serialize(Censor(values), VisualSerializerOptions));

        await stream.WriteAsync(OldProtocolMeta.Signature);
        using Aes aes = Aes.Create();
        aes.Key = RandomNumberGenerator.GetBytes(32);
        aes.IV = RandomNumberGenerator.GetBytes(16);

        await stream.WriteAsync(aes.Key);
        await stream.WriteAsync(aes.IV);
        await stream.WriteAsync(BitConverter.GetBytes(data.Length));
        ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using (CryptoStream cryptoStream = new(stream, encryptor, CryptoStreamMode.Write, true))
        {
            await cryptoStream.WriteAsync(data);
        }
        if (data.Length % 16 != 0)
            await stream.WriteAsync(RandomNumberGenerator.GetBytes(16)); // no fucking idea why it does that. im gonna gouge my eyes out with a spoon.


        byte[] hash = SHA256.HashData([.. data, .. aes.Key]);
        await stream.WriteAsync(hash);
    }

    public static Dictionary<string, string> Censor(IReadOnlyDictionary<string, string> values)
    {
        Dictionary<string, string> copy = new(values);

        foreach (var entry in (string[])["auth", "secret", "key", "invite-code"])
        {
            if (copy.TryGetValue(entry, out string? value))
                copy[entry] = CensorString(value);
        }

        return copy;
    }

    private static string CensorString(string secret)
    {
        return "<withheld>";
    }

    public static async Task<OldMessage> Deserialize(Stream stream, Logger? logger, CancellationToken cancellationToken = default)
    {
        using Logger.ContextTracker? contextTracker = logger?.LogContext(nameof(Deserialize));

        byte[] signatureBuffer = new byte[OldProtocolMeta.Signature.Length];
        await stream.ReadExactlyAsync(signatureBuffer, cancellationToken);
        if (!OldProtocolMeta.Signature.Span.SequenceEqual(signatureBuffer))
            throw new("invalid sig");

        using Aes aes = Aes.Create();
        byte[] aesBuffer = new byte[32 + 16];
        await stream.ReadExactlyAsync(aesBuffer, cancellationToken);
        (aes.Key, aes.IV) = (aesBuffer[0..32], aesBuffer[32..]);

        byte[] lengthBuffer = new byte[4];
        await stream.ReadExactlyAsync(lengthBuffer, cancellationToken);
        byte[] data = new byte[BitConverter.ToInt32(lengthBuffer)];

        ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using (CryptoStream cryptoStream = new(stream, decryptor, CryptoStreamMode.Read, true))
        {
            await cryptoStream.ReadExactlyAsync(data, cancellationToken);
        }

        byte[] hashBuffer = new byte[SHA256.HashSizeInBytes];
        await stream.ReadExactlyAsync(hashBuffer, cancellationToken);

        byte[] hash = SHA256.HashData([.. data, .. aes.Key]);

        if (!hash.SequenceEqual(hashBuffer))
            throw new("invalid hash");

        Dictionary<string, string> values = JsonSerializer.Deserialize<Dictionary<string, string>>(data, OldSerializer.JsonSerializerOptions) ?? throw new InvalidOperationException("failed to deserialize");

        contextTracker?.Write(JsonSerializer.Serialize(Censor(values), VisualSerializerOptions));

        return new(values);
    }

    public void Add(string key, string value)
    {
        ((IDictionary<string, string>)values).Add(key, value);
    }

    public bool ContainsKey(string key)
    {
        return ((IDictionary<string, string>)values).ContainsKey(key);
    }

    public bool Remove(string key)
    {
        return ((IDictionary<string, string>)values).Remove(key);
    }

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out string value)
    {
        return ((IDictionary<string, string>)values).TryGetValue(key, out value);
    }

    public void Add(KeyValuePair<string, string> item)
    {
        ((ICollection<KeyValuePair<string, string>>)values).Add(item);
    }

    public void Clear()
    {
        ((ICollection<KeyValuePair<string, string>>)values).Clear();
    }

    public bool Contains(KeyValuePair<string, string> item)
    {
        return ((ICollection<KeyValuePair<string, string>>)values).Contains(item);
    }

    public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex)
    {
        ((ICollection<KeyValuePair<string, string>>)values).CopyTo(array, arrayIndex);
    }

    public bool Remove(KeyValuePair<string, string> item)
    {
        return ((ICollection<KeyValuePair<string, string>>)values).Remove(item);
    }

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
    {
        return ((IEnumerable<KeyValuePair<string, string>>)values).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)values).GetEnumerator();
    }
}

[Obsolete]
public static class OldProtocolMeta
{
    public static ReadOnlyMemory<byte> Signature { get; } = Encoding.ASCII.GetBytes("TRNSMTR-A021"); // make private and expose a Sign(Stream) method

    public static bool ValidateSignature(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[Signature.Length];
        stream.ReadExactly(buffer);
        return Signature.Span.SequenceEqual(buffer);
    }

    public static void AssertSignature(Stream stream)
    {
        if (!ValidateSignature(stream))
            throw new ArgumentOutOfRangeException(nameof(stream), "Signature mismatch");
    }
}

// represents either an auth token acquired from a server, or an invite code needed to acquire said token
public readonly struct ClientSecret : IEquatable<ClientSecret>
{
    public const int SERIALIZED_LENGTH = 16;

    public bool IsAuthToken { get; }
    public ReadOnlyMemory<byte> Data { get; }

    private ClientSecret(bool isAuthToken, ReadOnlyMemory<byte> data)
    {
        IsAuthToken = isAuthToken;
        Data = data;
    }

    public void Serialize(Stream stream)
    {
        stream.WriteByte(Convert.ToByte(IsAuthToken));
        stream.Write(Data.Span);
    }

    public static ClientSecret EmitAuthToken() => Emit(true);
    public static ClientSecret EmitInviteCode() => Emit(false);
    private static ClientSecret Emit(bool isAuthToken) => new(isAuthToken, RandomNumberGenerator.GetBytes(SERIALIZED_LENGTH - 1));

    public ReadOnlyMemory<byte> Serialize()
    {
        byte[] result = new byte[SERIALIZED_LENGTH];
        using MemoryStream stream = new(result);
        Serialize(stream);
        return result;
    }

    public static ClientSecret Deserialize(Stream stream)
    {
        bool isAuthToken = Convert.ToBoolean(stream.ReadByte());
        byte[] buffer = new byte[SERIALIZED_LENGTH - 1];
        stream.ReadExactly(buffer);
        return new(isAuthToken, buffer);
    }

    public string Encode()
    {
        using MemoryStream stream = new();
        Serialize(stream);
        return Convert.ToBase64String(stream.ToArray());
    }

    public static ClientSecret Decode(string code)
    {
        byte[] data = Convert.FromBase64String(code);
        using MemoryStream stream = new(data);
        return Deserialize(stream);
    }

    public bool Equals(ClientSecret other) => IsAuthToken == other.IsAuthToken && Data.Span.SequenceEqual(other.Data.Span);
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is ClientSecret other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(IsAuthToken, Data.ToArray().Aggregate(0, (a, b) => HashCode.Combine(a, b)));
    public static bool operator ==(ClientSecret left, ClientSecret right) => left.Equals(right);
    public static bool operator !=(ClientSecret left, ClientSecret right) => !left.Equals(right);
}