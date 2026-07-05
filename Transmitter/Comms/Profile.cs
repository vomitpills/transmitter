using System.Security.Cryptography;
using System.Text;

using Transmitter.IO;

namespace Transmitter.Comms;

public class Profile(string displayName, ConsoleColor color, int beepFrequency) : ISerializable<Profile>
{
    public static IReadOnlyList<ConsoleColor> UserColors { get; } = [ConsoleColor.Green, ConsoleColor.Magenta, ConsoleColor.Blue, ConsoleColor.Cyan, ConsoleColor.DarkBlue, ConsoleColor.DarkMagenta, ConsoleColor.White, ConsoleColor.DarkYellow, ConsoleColor.Red];
    public static (int From, int To) BeepRange { get; } = new(200, 1000);
    public const byte MaxNameLengthBytes = byte.MaxValue;

    private static readonly Encoding nameEncoding = Encoding.UTF8;

    public static Profile RandomProfile => new(Random.Shared.GetHexString(12), UserColors[Random.Shared.Next(UserColors.Count)], BeepRange.From + Random.Shared.Next(BeepRange.To - BeepRange.From));

    public string DisplayName { get; } = displayName;
    public ConsoleColor Color { get; } = color;
    public int BeepFrequency { get; } = beepFrequency;

    public ReadOnlyMemory<byte> Hash { get; } = SHA256.HashData([.. Encoding.UTF8.GetBytes(displayName), (byte)color, .. BitConverter.GetBytes(beepFrequency)]);

    public void Serialize(BinaryWriter writer)
    {
        // add self versioning
        byte[] nameBuffer = nameEncoding.GetBytes(DisplayName);
        if (nameBuffer.Length > MaxNameLengthBytes)
            throw new InvalidOperationException($"Invalid name length ('{nameBuffer.Length}' > {MaxNameLengthBytes})");
        writer.Write((byte)nameBuffer.Length);
        writer.Write(nameBuffer);
        writer.Write((byte)Color);
        writer.Write(BeepFrequency);
    }

    public static Profile Deserialize(BinaryReader reader)
    {
        // add self versioning
        byte nameLength = reader.ReadByte();
        Span<byte> nameBuffer = stackalloc byte[nameLength];
        reader.ReadExactly(nameBuffer);
        string name = nameEncoding.GetString(nameBuffer);
        ConsoleColor color = (ConsoleColor)reader.ReadByte();
        if (!UserColors.Contains(color))
            throw new InvalidDataException($"Illegal user color: '{color}'");
        int beepFrequency = reader.ReadInt32();
        if (beepFrequency < BeepRange.From || beepFrequency > BeepRange.To)
            throw new InvalidDataException($"Illegal beep frequency: '{beepFrequency}' not in [{BeepRange.From};{BeepRange.To}]");
        return new(name, color, beepFrequency);
    }
}
