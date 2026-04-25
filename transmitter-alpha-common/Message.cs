using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace transmitter_alpha_common;

public class Message : IDictionary<string, string>
{
    public Message()
    {

    }

    public Message(Dictionary<string, string> values)
    {
        this.values = values;
    }

    private readonly Dictionary<string, string> values = [];

    public ICollection<string> Keys => ((IDictionary<string, string>)values).Keys;

    public ICollection<string> Values => ((IDictionary<string, string>)values).Values;

    public int Count => ((ICollection<KeyValuePair<string, string>>)values).Count;

    public bool IsReadOnly => ((ICollection<KeyValuePair<string, string>>)values).IsReadOnly;

    public string this[string key] { get => ((IDictionary<string, string>)values)[key]; set => ((IDictionary<string, string>)values)[key] = value; }

    private static readonly JsonSerializerOptions visualSerializerOptions = new(Serializer.JsonSerializerOptions) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public async Task Serialize(Stream stream, Logger? logger)
    {
        logger ??= Logger.Shared;
        using Logger.ContextTracker contextTracker = logger.LogContext(nameof(Serialize));
        string json = JsonSerializer.Serialize(values, Serializer.JsonSerializerOptions);
        byte[] data = Encoding.UTF8.GetBytes(json);

        contextTracker.Write(JsonSerializer.Serialize(Censor(values), visualSerializerOptions));

        await stream.WriteAsync(ProtocolMeta.Signature);
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

    private static Dictionary<string, string> Censor(IReadOnlyDictionary<string, string> values)
    {
        Dictionary<string, string> copy = new(values);

        foreach (var entry in (string[])["auth", "secret", "key", "invite-code"])
        {
            if (copy.TryGetValue(entry, out string? value))
                copy[entry] = CensorString(value);
        }

        return copy;
    }

    private static string CensorString(string secret) => "<withheld>";

    public static async Task<Message> Deserialize(Stream stream, Logger? logger, CancellationToken cancellationToken = default)
    {
        logger ??= Logger.Shared;
        using Logger.ContextTracker contextTracker = logger.LogContext(nameof(Deserialize));

        byte[] signatureBuffer = new byte[ProtocolMeta.Signature.Length];
        await stream.ReadExactlyAsync(signatureBuffer, cancellationToken);
        if (!ProtocolMeta.Signature.SequenceEqual(signatureBuffer))
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

        Dictionary<string, string> values = JsonSerializer.Deserialize<Dictionary<string, string>>(data, Serializer.JsonSerializerOptions) ?? throw new InvalidOperationException("failed to deserialize");

        contextTracker.Write(JsonSerializer.Serialize(Censor(values), visualSerializerOptions));

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

internal static class ProtocolMeta
{
    public static byte[] Signature = Encoding.ASCII.GetBytes("TRNSMTR-A01");
}