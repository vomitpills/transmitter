using System.Text;
using Transmitter.Comms.Requests;

namespace Transmitter.Comms.Gateways;

public sealed class Connection : IDisposable // add keepalive packet logic
{
    private readonly BinaryWriter writer;
    private readonly BinaryReader reader;

    public Connection(ConnectionOptions connectionOptions, BinaryWriter writer, BinaryReader reader)
    {
        this.writer = writer;
        this.reader = reader;
        ConnectionOptions = connectionOptions;
    }

    public ConnectionOptions ConnectionOptions { get; }

    public async Task<Response> RequestAsync(Request request, Func<ResponsePayloadType, ResponseDeserializationFinalizer> finalizerChooser, CancellationToken cancellationToken = default)
    {
        await WriteRequestAsync(request, cancellationToken);
        return await ReadResponseAsync(finalizerChooser, cancellationToken);
    }

    private async Task WriteRequestAsync(Request request, CancellationToken cancellationToken = default) => request.Serialize(writer);
    private async Task<Response> ReadResponseAsync(Func<ResponsePayloadType, ResponseDeserializationFinalizer> finalizerChooser, CancellationToken cancellationToken = default) => Response.BeginDeserialization(reader, finalizerChooser);

    public async Task<Request> ReadRequestAsync(Func<RequestIntention, RequestDeserializationFinalizer> finalizerChooser, CancellationToken cancellationToken = default) => Request.BeginDeserialization(reader, finalizerChooser);
    public async Task WriteResponseAsync(Response response, CancellationToken cancellationToken = default) => response.Serialize(writer);

    //private async Task SendKeepAlive(Stream stream)
    //{
    //    if (commSemaphore.CurrentCount is 0)
    //        return;

    //    commSemaphore.Wait();
    //    try
    //    {
    //        OldMessage keepAliveRequest = WithAuth(new()
    //        {
    //            ["intent"] = "ping"
    //        });
    //        await keepAliveRequest.Serialize(stream, null);

    //        OldMessage keepAliveResponse = await OldMessage.Deserialize(stream, null);

    //        switch (keepAliveResponse["status"])
    //        {
    //            case "pong":
    //                break;
    //            case "err":
    //                throw CommonException.Deserialize(keepAliveResponse["cause"]);
    //            default:
    //                throw new MessageStructureException("status");
    //        }
    //    }
    //    finally
    //    {
    //        commSemaphore.Release();
    //    }
    //}

    public void Dispose()
    {
        writer.Dispose();
        reader.Dispose();
    }
}

// check roles for undefined values
// add encryption schemes
// add configurable error correction
public readonly struct ConnectionOptions : ISerializable<ConnectionOptions>
{
    public Guid Guid { get; }
    public Role Role { get; private init; }
    public short KeepaliveIntervalSeconds { get; }

    private ConnectionOptions(Guid guid, Role initiatorRole, short keepaliveIntervalSeconds)
    {
        Guid = guid;
        Role = initiatorRole;
        KeepaliveIntervalSeconds = keepaliveIntervalSeconds;
    }

    public ConnectionOptions Reversed() => this with 
    {
        Role = Role switch
        {
            Role.Client => Role.Server,
            Role.Server => Role.Client,
            _ => throw new InvalidOperationException($"Failed to reverse {nameof(Role)} '{Role}'")
        }
    };

    public ConnectionOptions(Role initiatorRole, short keepaliveIntervalSeconds) : this(Guid.NewGuid(), initiatorRole, keepaliveIntervalSeconds) { }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(Guid.ToByteArray());
        writer.Write((byte)Role);
        writer.Write(KeepaliveIntervalSeconds);
    }

    public static ConnectionOptions Deserialize(BinaryReader reader)
    {
        Span<byte> buffer = stackalloc byte[16];
        reader.ReadExactly(buffer);
        Guid guid = new(buffer);
        Role role = (Role)reader.ReadByte();
        if (!Enum.IsDefined(role))
            throw new InvalidDataException($"'{role}' is not defined for {nameof(Gateways.Role)}");
        short keepaliveIntervalSeconds = reader.ReadInt16();
        return new(guid, role, keepaliveIntervalSeconds);
    }
}

public enum Role : byte
{
    Client = 0, Server = byte.MaxValue
}