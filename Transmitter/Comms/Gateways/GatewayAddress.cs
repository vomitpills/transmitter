using System.Net;

using Transmitter.IO;

namespace Transmitter.Comms.Gateways;

public abstract class GatewayAddress { }

public class WebGatewayAddress(IPEndPoint ipEndPoint) : GatewayAddress, ISerializable<WebGatewayAddress> // should also support ipv6
{
    public WebGatewayAddress(IPAddress ipAddress, ushort port) : this(new(ipAddress, port)) { }

    public IPEndPoint IPEndPoint { get; } = ipEndPoint;

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(IPEndPoint.Address.GetAddressBytes());
        writer.Write((ushort)IPEndPoint.Port);
    }
    public static WebGatewayAddress Deserialize(BinaryReader reader)
    {
        Span<byte> buffer = stackalloc byte[4];
        reader.ReadExactly(buffer);
        IPEndPoint endPoint = new(new IPAddress(buffer), reader.ReadUInt16());
        return new(endPoint);
    }
}