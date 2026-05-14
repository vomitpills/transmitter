using System.Text;

namespace Transmitter.Comms;

internal static class ProtocolMeta
{
    private const string SIGNATURE = "TMT";
    private const ushort PROTOCOL_VERSION = 0;

#if CLOSED
    private const bool CLOSED = true;
#else
    private const bool CLOSED = false;
#endif

    private static ReadOnlyMemory<byte> Header { get; } = (byte[])[.. Encoding.ASCII.GetBytes(SIGNATURE), .. BitConverter.GetBytes(PROTOCOL_VERSION)];

    public static void WriteHeader(BinaryWriter writer) => writer.Write(Header.Span);
    public static void ReadHeader(BinaryReader reader)
    {
        Span<byte> headerBuffer = stackalloc byte[Header.Length];
        reader.ReadExactly(headerBuffer);
        if (!Header.Span.SequenceEqual(headerBuffer))
            throw new InvalidDataException("Incompatible header");
    }
}