namespace Transmitter.Comms.Gateways;

public abstract class Gateway : IDisposable
{
    public delegate Task IncomingTerminalAcceptedHandler(BinaryReader reader, ConnectionOptions options);
    public delegate Task OutgoingTerminalAcceptedHandler(BinaryWriter writer, ConnectionOptions options);
    public event IncomingTerminalAcceptedHandler? IncomingTerminalAccepted;
    public event OutgoingTerminalAcceptedHandler? OutgoingTerminalAccepted;

    protected async Task<ConnectionOptions?> OnIncoming(BinaryReader reader, CancellationToken cancellationToken = default)
    {
        if (IncomingTerminalAccepted is null)
        {
            reader.Dispose();
            return null;
        }
        ConnectionOptions options = ConnectionOptions.Deserialize(reader).Reversed();
        await IncomingTerminalAccepted.Invoke(reader, options);
        return options;
    }

    protected async Task OnOutgoing(BinaryWriter writer, ConnectionOptions options, CancellationToken cancellationToken = default)
    {
        if (OutgoingTerminalAccepted is null)
        {
            writer.Dispose();
            return;
        }
        options.Serialize(writer);
        await OutgoingTerminalAccepted.Invoke(writer, options);
    }

    //[RequiresStopped]
    public abstract void Start();
    //[RequiresStarted]
    public abstract void Stop();

    public abstract void Dispose();
}