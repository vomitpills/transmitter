using System.Net;
using System.Text;

namespace transmitter_alpha_common;

public readonly struct ClientBootstrapInfo(IPAddress serverAddress, ushort commPort, ushort notifPort, ClientSecret clientSecret)
{
    public IPAddress ServerAddress { get; } = serverAddress;
    public ushort CommPort { get; } = commPort;
    public ushort ReturnLinePort { get; } = notifPort;
    public ClientSecret ClientSecret { get; init; } = clientSecret;

    public void Serialize(Stream stream)
    {
        stream.Write(ProtocolMeta.Signature.Span);
        stream.Write(ServerAddress.GetAddressBytes());
        stream.Write(BitConverter.GetBytes(CommPort));
        stream.Write(BitConverter.GetBytes(ReturnLinePort));
        ClientSecret.Serialize(stream);
    }

    public static ClientBootstrapInfo Deserialize(Stream stream)
    {
        ProtocolMeta.AssertSignature(stream);

        Span<byte> addressBuffer = stackalloc byte[4];
        stream.ReadExactly(addressBuffer);
        IPAddress address = new(addressBuffer);

        using BinaryReader reader = new(stream, Encoding.Default, true);
        (ushort commPort, ushort notifPort) = (reader.ReadUInt16(), reader.ReadUInt16());

        ClientSecret secret = ClientSecret.Deserialize(stream);

        return new(address, commPort, notifPort, secret);
    }

    public string Encode()
    {
        using MemoryStream stream = new();
        Serialize(stream);
        return Convert.ToBase64String(stream.ToArray());
    }

    public static ClientBootstrapInfo Decode(string code)
    {
        byte[] data = Convert.FromBase64String(code);
        using MemoryStream stream = new(data);
        return Deserialize(stream);
    }
}