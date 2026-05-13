using System.Text;

namespace Transmitter;

public class Profile : ISerializable<Profile>
{
    private static readonly Encoding nameEncoding = Encoding.UTF8;

    public static (int From, int To) BeepRange { get; } = new(200, 1000);
    public const byte MaxNameLength = 16;
    public static IReadOnlyList<ConsoleColor> UserColors { get; } = [ConsoleColor.Green, ConsoleColor.Magenta, ConsoleColor.Blue, ConsoleColor.Cyan, ConsoleColor.DarkBlue, ConsoleColor.DarkMagenta, ConsoleColor.White, ConsoleColor.DarkYellow, ConsoleColor.Red];

    public string DisplayName { get; }
    public ConsoleColor Color { get; }
    public int BeepFrequency { get; }

    public Profile(string displayName, ConsoleColor color, int beepFrequency)
    {
        if (displayName.Length is not (> 0 and < MaxNameLength))
            throw new ArgumentOutOfRangeException(nameof(displayName));

        if (!UserColors.Contains(color))
            throw new ArgumentOutOfRangeException(nameof(color));

        if (beepFrequency < BeepRange.From || beepFrequency > BeepRange.To)
            throw new ArgumentOutOfRangeException(nameof(beepFrequency));

        DisplayName = displayName;
        Color = color;
        BeepFrequency = beepFrequency;
    }

    public void Serialize(BinaryWriter writer)
    {
        // add self versioning
        byte[] nameBuffer = nameEncoding.GetBytes(DisplayName);
        writer.Write((byte)nameBuffer.Length);
        writer.Write(nameBuffer);
        writer.Write((byte)Color);
        writer.Write(BitConverter.GetBytes(BeepFrequency));
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
            throw new ArgumentOutOfRangeException(nameof(reader), "Illegal user color");
        int beepFrequency = reader.ReadInt32();
        if (beepFrequency < BeepRange.From || beepFrequency > BeepRange.To)
            throw new ArgumentOutOfRangeException(nameof(reader), "Illegal beep frequency");
        return new(name, color, beepFrequency);
    }
}
