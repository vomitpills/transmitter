using System.Security.Cryptography;

namespace Transmitter.Security;

public readonly struct HashedId : IEquatable<HashedId> // potential security hazard lmao
{
    public const ushort KEY_LENGTH = 48;
    private readonly ReadOnlyMemory<byte> hash;

    private HashedId(ReadOnlyMemory<byte> hash) => this.hash = hash;

    public static ReadOnlySpan<byte> Emit(out HashedId result)
    {
        ReadOnlySpan<byte> key = RandomNumberGenerator.GetBytes(KEY_LENGTH).AsSpan();
        result = FromKey(key);
        return key;
    }

    public static HashedId FromKey(ReadOnlySpan<byte> key) => new(Hash(key));

    public bool Validate(ReadOnlySpan<byte> key) => hash.Span.SequenceEqual(Hash(key).Span);

    public void SerializeHash(BinaryWriter writer)
    {
        writer.Write((ushort)hash.Length);
        writer.Write(hash.Span);
    }
    public static void SerializeKey(BinaryWriter writer, ReadOnlySpan<byte> key)
    {
        if (key.Length is not KEY_LENGTH)
            throw new ArgumentOutOfRangeException(nameof(key));
        writer.Write((ushort)key.Length);
        writer.Write(key);
    }

    public static HashedId DeserializeHash(BinaryReader reader)
    {
        ushort length = reader.ReadUInt16();
        byte[] hash = new byte[length];
        reader.ReadExactly(hash);
        return new HashedId(hash);
    }
    public static ReadOnlyMemory<byte> DeserializeKey(BinaryReader reader)
    {
        ushort length = reader.ReadUInt16();
        if (length is not KEY_LENGTH)
            throw new InvalidDataException($"Invalid passcode length: {length} (expected {KEY_LENGTH})");
        byte[] key = new byte[length];
        reader.ReadExactly(key);
        return key;
    }

    private static ReadOnlyMemory<byte> Hash(ReadOnlySpan<byte> key) => SHA256.HashData(key);

    public bool Equals(HashedId other) => other.hash.Span.SequenceEqual(hash.Span);
    public override bool Equals(object? obj) => obj is HashedId passcode && Equals(passcode);
    public override int GetHashCode()
    {
        HashCode hashCode = default;
        hashCode.AddBytes(hash.Span);
        return hashCode.ToHashCode();
    }

    public static bool operator ==(HashedId a, HashedId b) => a.Equals(b);
    public static bool operator !=(HashedId a, HashedId b) => !a.Equals(b);
}