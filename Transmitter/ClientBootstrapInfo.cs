using System.Net;
using System.Text;
using Transmitter.Authentication;

namespace Transmitter;

[Obsolete]
public readonly struct ClientBootstrapInfo(IPAddress serverAddress, ushort commPort, ushort notifPort, ClientSecret clientSecret) : ISerializable<ClientBootstrapInfo>
{
    public IPAddress ServerAddress { get; } = serverAddress;
    public ushort CommPort { get; } = commPort;
    public ushort ReturnLinePort { get; } = notifPort;

    public ClientSecret ClientSecret { get; init; } = clientSecret;

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(ServerAddress.GetAddressBytes());
        writer.Write(BitConverter.GetBytes(CommPort));
        writer.Write(BitConverter.GetBytes(ReturnLinePort));
        ClientSecret.Serialize(writer);
    }

    public static ClientBootstrapInfo Deserialize(BinaryReader reader)
    {
        Span<byte> addressBuffer = stackalloc byte[4];
        reader.ReadExactly(addressBuffer);
        IPAddress address = new(addressBuffer);
        (ushort commPort, ushort notifPort) = (reader.ReadUInt16(), reader.ReadUInt16());
        ClientSecret secret = ClientSecret.Deserialize(reader);
        return new(address, commPort, notifPort, secret);
    }
}