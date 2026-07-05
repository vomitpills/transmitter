using System.Net;

using Transmitter.Comms;
using Transmitter.Comms.Gateways;
using Transmitter.Security;

namespace Transmitter.Client; 

public partial class Client : Node
{
    private readonly PersistentClientState persistentState;
    
    protected override Dictionary<Guid, InstanceConnection> InstanceConnections { get; }

    private Task? connectionManager;

    public Client(PersistentClientState state)
    {
        persistentState = state;
        TcpGateway gateway = new(new(IPAddress.Any, 0), new("CLIENT-GATEWAY"));
        Mount(gateway);
        gateway.Start();

        InstanceConnections = persistentState.Instances.Select(i => (i.Key, InstanceConnection.Create(ConnectAsync, i.Value))).ToDictionary();

        connectionManager = Task.Run(async () =>
        {
            foreach (var item in InstanceConnections)
                await item.Value.Start();
        }); // bandaid
    }

    public Task Relay(Request request, Guid chatId)
    {
        if (!persistentState.ChatMembers.TryGetValue(chatId, out var members))
            throw new KeyNotFoundException($"No chat with id '{chatId}'");

        // make a WAL abt transaction

        List<Task> tasks = [];
        foreach (var item in members)
        {
            Task sendTask = Task.Run(() => Relay(request, persistentState.Recipients[item]));
            tasks.Add(sendTask);
        }
        return Task.WhenAll(tasks);
    }
}