using System.Net;
using System.Net.Sockets;

namespace Transmitter.Comms.Gateways;

public class TcpGateway(IPEndPoint localEndPoint) : Gateway
{
    private TcpListener Listener { get; } = new(localEndPoint);
    private Task? clientLoopTask;
    private Task? cancelClientLoopTask;

    public async Task InitiateConnectionAsync(IPEndPoint endPoint, ConnectionOptions options, CancellationToken cancellationToken = default)
    {
        TcpClient client = new();
        await client.ConnectAsync(endPoint);
        await OnOutgoing(new(client.GetStream()), options, cancellationToken);
        await OnIncoming(new(client.GetStream()), cancellationToken);
    }

    public override void Start()
    {
        if (clientLoopTask is not null)
            throw new InvalidOperationException("Already running");

        CancellationTokenSource cancellationTokenSource = new();
        TaskCompletionSource taskCompletionSource = new();
        clientLoopTask = Task.Factory.StartNew(async () =>
        {
            try
            {
                Listener.Start();
                while (!cancellationTokenSource.IsCancellationRequested)
                {
                    TcpClient tcpClient = await Listener.AcceptTcpClientAsync(cancellationTokenSource.Token);
                    ConnectionOptions? incomingConnectionOptions = await OnIncoming(new(tcpClient.GetStream()), cancellationTokenSource.Token);
                    if (incomingConnectionOptions is not ConnectionOptions options)
                    {
                        tcpClient.Dispose();
                        continue;
                    }
                    await OnOutgoing(new(tcpClient.GetStream()), options, cancellationTokenSource.Token);
                }
            }
            finally
            {
                Listener.Stop();
                clientLoopTask = null;
                cancelClientLoopTask = null;
                cancellationTokenSource.Dispose();
                taskCompletionSource.SetResult();
            }
        }, TaskCreationOptions.LongRunning);
        cancelClientLoopTask = taskCompletionSource.Task;
    }

    public override void Stop()
    {
        if (clientLoopTask is null)
            throw new InvalidOperationException("Already stopped");
        cancelClientLoopTask!.Wait(); // prolly a bad idea
    }

    public override void Dispose() => GC.SuppressFinalize(this);
}