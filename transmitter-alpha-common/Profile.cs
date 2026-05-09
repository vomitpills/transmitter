using System.Text;

namespace transmitter_alpha_common;

public class Profile
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

    public void Serialize(Stream stream)
    {
        stream.Write(ProtocolMeta.Signature.Span); // change to self versioning
        stream.WriteByte((byte)DisplayName.Length);
        stream.Write(nameEncoding.GetBytes(DisplayName));
        stream.WriteByte((byte)Color);
        stream.Write(BitConverter.GetBytes(BeepFrequency));
    }

    public static Profile Deserialize(Stream stream)
    {
        ProtocolMeta.ValidateSignature(stream);
        byte nameLength = (byte)stream.ReadByte();
        Span<byte> nameBuffer = stackalloc byte[nameLength];
        stream.ReadExactly(nameBuffer);
        string name = nameEncoding.GetString(nameBuffer);
        ConsoleColor color = (ConsoleColor)stream.ReadByte();
        if (!UserColors.Contains(color))
            throw new ArgumentOutOfRangeException(nameof(stream), "Illegal user color");
        using BinaryReader reader = new(stream, Encoding.Default, true);
        int beepFrequency = reader.ReadInt32();
        if (beepFrequency < BeepRange.From || beepFrequency > BeepRange.To)
            throw new ArgumentOutOfRangeException(nameof(stream), "Illegal beep frequency");
        return new(name, color, beepFrequency);
    }
}
