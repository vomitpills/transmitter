using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Transmitter;

namespace Transmitter.Authentication;

public readonly struct ClientSecret : IEquatable<ClientSecret>, ISerializable<ClientSecret>
{
    public static byte Length { get; } = 32;
    private readonly ReadOnlyMemory<byte> data;

    private ClientSecret(ReadOnlyMemory<byte> data) => this.data = data;

    public static ClientSecret Emit() => new(RandomNumberGenerator.GetBytes(Length));

    public void Serialize(BinaryWriter writer) => writer.Write(data.Span);

    public static ClientSecret Deserialize(BinaryReader reader)
    {
        Span<byte> buffer = stackalloc byte[Length];
        reader.ReadExactly(buffer);
        return new();
    }

    public bool Equals(ClientSecret other) => data.Span.SequenceEqual(other.data.Span);
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is ClientSecret other && Equals(other);
    public override int GetHashCode() => data.ToArray().Aggregate(0, (a, b) => HashCode.Combine(a, b));

    public static bool operator ==(ClientSecret left, ClientSecret right) => left.Equals(right);
    public static bool operator !=(ClientSecret left, ClientSecret right) => !left.Equals(right);
}