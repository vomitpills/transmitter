namespace Transmitter.Comms.Gateways;

public abstract class Transceiver(Predicate<ConnectionOptions>? connectionFilter = null)
{
    #region sequential
    private readonly Lock terminalsLock = new();
    private readonly Dictionary<Guid, BinaryReader> IncomingTerminals = [];
    private readonly Dictionary<Guid, BinaryWriter> OutgoingTerminals = [];
    #endregion

    protected readonly HashSet<IGateway> gatewayRack = [];

    protected event Action<QAConnection>? NewConnection;

    protected async Task<QAConnection?> ConnectAsync(GatewayAddress address, ConnectionOptions options, CancellationToken cancellationToken = default)
    {
        IList<IGateway> gateways = [.. gatewayRack.Where(g => g.IsSuitable(address))];
        if (gateways.Count is 0)
            return null;
        IGateway gateway = gateways[Random.Shared.Next(gateways.Count)];

        #region task prep
        TaskCompletionSource<QAConnection> connectionEstablishedTask = new();

        void Callback(QAConnection connection)
        {
            NewConnection -= Callback;
            connectionEstablishedTask.TrySetResult(connection);
        }

        cancellationToken.Register(() =>
        {
            NewConnection -= Callback;
            connectionEstablishedTask.SetCanceled(cancellationToken);
        });

        NewConnection += Callback;
        #endregion

        await gateway.InitiateConnection(address, options, cancellationToken);
        return await connectionEstablishedTask.Task;
    }

    public void Mount<T>(Gateway<T> gateway) where T : GatewayAddress
    {
        gatewayRack.Add(gateway);
        gateway.IncomingTerminalAccepted += OnIncomingAccepted;
        gateway.OutgoingTerminalAccepted += OnOutgoingAccepted;
    }

    public void Unmount<T>(Gateway<T> gateway) where T : GatewayAddress
    {
        gatewayRack.Remove(gateway);
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
