namespace Transmitter.Comms.Gateways;

public abstract class Transceiver(Predicate<ConnectionOptions>? connectionFilter = null)
{
    #region sequential
    private readonly Lock terminalsLock = new();
    private readonly Dictionary<Guid, BinaryReader> IncomingTerminals = [];
    private readonly Dictionary<Guid, BinaryWriter> OutgoingTerminals = [];
    #endregion

    protected event Action<QAConnection>? NewConnection;

    protected async Task<QAConnection> ConnectAsync(Func<Task> sendTask, CancellationToken cancellationToken = default)
    {
        TaskCompletionSource<QAConnection> completionSource = new();

        void Callback(QAConnection connection)
        {
            NewConnection -= Callback;
            completionSource.TrySetResult(connection);
        }

        cancellationToken.Register(() =>
        {
            NewConnection -= Callback;
            completionSource.SetCanceled(cancellationToken);
        });

        NewConnection += Callback;
        await sendTask();
        return await completionSource.Task;
    }

    public void Mount(Gateway gateway)
    {
        gateway.IncomingTerminalAccepted += OnIncomingAccepted;
        gateway.OutgoingTerminalAccepted += OnOutgoingAccepted;
    }

    public void Unmount(Gateway gateway)
    {
        gateway.IncomingTerminalAccepted -= OnIncomingAccepted;
        gateway.OutgoingTerminalAccepted -= OnOutgoingAccepted;
    }

    private async Task OnIncomingAccepted(BinaryReader reader, ConnectionOptions options)
    {
        if (connectionFilter is not null && !connectionFilter(options))
        {
            reader.Dispose();
            return;
        }
        lock (terminalsLock)
        {
            if (OutgoingTerminals.Remove(options.Guid, out BinaryWriter? writer))
                NewConnection?.Invoke(new(options, writer, reader, new("connection-" + Random.Shared.GetHexString(4))));
            else
                IncomingTerminals.Add(options.Guid, reader); // remove after timeout
        }
    }

    private async Task OnOutgoingAccepted(BinaryWriter writer, ConnectionOptions options)
    {
        if (connectionFilter is not null && !connectionFilter(options))
        {
            writer.Dispose();
            return;
        }
        lock (terminalsLock)
        {
            if (IncomingTerminals.Remove(options.Guid, out BinaryReader? reader))
                NewConnection?.Invoke(new(options, writer, reader, new("connection-" + Random.Shared.GetHexString(4))));
            else
                OutgoingTerminals.Add(options.Guid, writer); // remove after timeout
        }
    }
}
