using Transmitter.Comms.Requests;
using Transmitter.Comms.Responses;
using Transmitter.IO;

namespace Transmitter.Comms.Gateways;

public sealed class QAConnection : IDisposable
{
    private readonly BinaryWriter writer;
    private readonly BinaryReader reader;
    private readonly ConnectionLogger? logger;

    private readonly SemaphoreSlim writeSemaphore = new(1, 1);
    private readonly CancellationTokenSource bgTasksCancellation = new();

    private readonly ChannelDemuxer<ChannelId, Action> requestDemuxer = new(1);

    public ConnectionOptions ConnectionOptions { get; }

    private DateTimeOffset lastRequestTime;

    public QAConnection(ConnectionOptions connectionOptions, BinaryWriter writer, BinaryReader reader, ConnectionLogger? logger = null)
    {
        this.writer = writer;
        this.reader = reader;
        this.logger = logger;
        ConnectionOptions = connectionOptions;

        Task.Factory.StartNew(async () =>
        {
            while (!bgTasksCancellation.IsCancellationRequested)
            {
                ChannelId channelId = (ChannelId)reader.ReadSByte();
                Action callback = await requestDemuxer.ReadAsync(channelId);
                callback();
            }
        }, TaskCreationOptions.LongRunning);

        if (connectionOptions.Role is Role.Client)
            Task.Factory.StartNew(async () =>
            {
                TimeSpan delay = connectionOptions.KeepAliveInterval;
                while (!bgTasksCancellation.IsCancellationRequested)
                {
                    await Task.Delay(delay, bgTasksCancellation.Token); // make the delay adaptive
                    TimeSpan intervalDifference = DateTimeOffset.UtcNow - lastRequestTime;
                    if (intervalDifference < connectionOptions.KeepAliveInterval)
                    {
                        delay = intervalDifference;
                        continue;
                    }
                    else
                        delay = connectionOptions.KeepAliveInterval;
                    await RequestAsync(ChannelId.KeepAlive, new KeepAliveRequest(), bgTasksCancellation.Token);
                }
            }, TaskCreationOptions.LongRunning);
        else
            Task.Factory.StartNew(async () =>
            {
                while (!bgTasksCancellation.IsCancellationRequested)
                {
                    Request request = await ReadRequestAsync(ChannelId.KeepAlive, (intention) => intention switch
                    {
                        RequestIntention.KeepAlive => KeepAliveRequest.FinalizeDeserialization,
                        _ => throw new ArgumentOutOfRangeException(nameof(intention))
                    }, bgTasksCancellation.Token);
                    await WriteResponseAsync(ChannelId.KeepAlive, new EmptyResponse(RequestStatus.OK), bgTasksCancellation.Token);
                }
            }, TaskCreationOptions.LongRunning);
    }

    public Task<Response> RequestAsync<T>(T request, CancellationToken cancellationToken = default) where T : Request, IRequest => RequestAsync(ChannelId.QA, request, cancellationToken);
    private async Task<Response> RequestAsync<T>(ChannelId channel, T request, CancellationToken cancellationToken = default) where T : Request, IRequest
    {
        await WriteRequestAsync(channel, request, cancellationToken);
        return await ReadResponseAsync(channel, T.FinalizeResponseDeserialization, cancellationToken);
    }

    private async Task WriteRequestAsync(ChannelId channel, Request request, CancellationToken cancellationToken = default)
    {
        await writeSemaphore.WaitAsync(cancellationToken);
        try
        {
            writer.Write((sbyte)channel);
            request.Serialize(writer, logger);
            writer.Flush();
            lastRequestTime = DateTimeOffset.UtcNow;
            logger?.Log($"flushed on {channel}");
        }
        finally
        {
            writeSemaphore.Release();
        }
    }
    private async Task WriteResponseAsync(ChannelId channel, Response response, CancellationToken cancellationToken = default)
    {
        await writeSemaphore.WaitAsync(cancellationToken);
        try
        {
            writer.Write((sbyte)channel);
            response.Serialize(writer, logger);
            writer.Flush();
            lastRequestTime = DateTimeOffset.UtcNow;
            logger?.Log($"flushed on {channel}");
        }
        finally
        {
            writeSemaphore.Release();
        }
    }
    public Task RespondAsync(Response response, CancellationToken cancellationToken = default) => WriteResponseAsync(ChannelId.QA, response, cancellationToken);

    private async Task<Request> ReadRequestAsync(ChannelId channel, Func<RequestIntention, RequestDeserializationFinalizer> finalizerChooser, CancellationToken cancellationToken = default) // seems pretty scuffed but let's roll with it
    {
        TaskCompletionSource<Request> completionSource = new();
        await requestDemuxer.WriteAsync(channel, () =>
        {
            Request request = Request.BeginDeserialization(reader, finalizerChooser, logger);
            completionSource.SetResult(request);
        }, cancellationToken);
        return await completionSource.Task;
    }
    private async Task<Response> ReadResponseAsync(ChannelId channel, Func<ResponsePayloadType, ResponseDeserializationFinalizer> finalizerChooser, CancellationToken cancellationToken = default)
    {
        TaskCompletionSource<Response> completionSource = new();
        await requestDemuxer.WriteAsync(channel, () =>
        {
            Response response = Response.BeginDeserialization(reader, finalizerChooser, logger);
            completionSource.SetResult(response);
        }, cancellationToken);
        return await completionSource.Task;
    }
    public Task<Request> AcceptRequestAsync(Func<RequestIntention, RequestDeserializationFinalizer> finalizerChooser, CancellationToken cancellationToken = default) => ReadRequestAsync(ChannelId.QA, finalizerChooser, cancellationToken);

    public void Dispose()
    {
        writer.Dispose();
        reader.Dispose();
        bgTasksCancellation.Cancel();
        bgTasksCancellation.Dispose();
    }

    private enum ChannelId : sbyte
    {
        KeepAlive = -1,
        QA = 0
    }
}

// check roles for undefined values
// add encryption schemes
// add configurable error correction
public readonly struct ConnectionOptions : ISerializable<ConnectionOptions>
{
    public Guid Guid { get; }
    public Role Role { get; private init; }
    public short KeepAliveIntervalSeconds { get; }
    public bool SkipKeepAliveIfRecentActivity { get; }

    public TimeSpan KeepAliveInterval => TimeSpan.FromSeconds(KeepAliveIntervalSeconds);

    private ConnectionOptions(Guid guid, Role initiatorRole, short keepAliveIntervalSeconds, bool skipKeepAliveIfRecentActivity = true)
    {
        Guid = guid;
        Role = initiatorRole;
        KeepAliveIntervalSeconds = keepAliveIntervalSeconds;
        SkipKeepAliveIfRecentActivity = skipKeepAliveIfRecentActivity;
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

    public ConnectionOptions(Role initiatorRole, short keepAliveIntervalSeconds, bool skipKeepAliveIfRecentActivity = true) : this(Guid.NewGuid(), initiatorRole, keepAliveIntervalSeconds, skipKeepAliveIfRecentActivity) { }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(Guid.ToByteArray());
        writer.Write((byte)Role);
        writer.Write(KeepAliveIntervalSeconds);
        writer.Write(SkipKeepAliveIfRecentActivity);
    }

    public static ConnectionOptions Deserialize(BinaryReader reader)
    {
        Span<byte> buffer = stackalloc byte[16];
        reader.ReadExactly(buffer);
        Guid guid = new(buffer);
        Role role = (Role)reader.ReadByte();
        if (!Enum.IsDefined(role))
            throw new InvalidDataException($"'{role}' is not defined for {nameof(Gateways.Role)}");
        short keepAliveIntervalSeconds = reader.ReadInt16();
        bool skipKeepAliveIfRecentActivity = reader.ReadBoolean();
        return new(guid, role, keepAliveIntervalSeconds, skipKeepAliveIfRecentActivity);
    }
}

public enum Role : byte
{
    Client = 0, Server = byte.MaxValue
}