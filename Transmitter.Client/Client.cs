using System.Net;

using Transmitter.Comms;
using Transmitter.Comms.Gateways;
using Transmitter.Security;

namespace Transmitter.Client; 

public partial class Client : Node  // rename to Node; Client should contain high level logic
{
    private readonly PersistentClientState persistentState;
    private readonly Dictionary<Guid, InstanceConnection> instanceConnections;

    private Task? connectionManager;

    public Client(PersistentClientState state)
    {
        persistentState = state;
        TcpGateway gateway = new(new(IPAddress.Any, 0), new("CLIENT-GATEWAY"));
        Mount(gateway);
        gateway.Start();

        instanceConnections = persistentState.Instances.Select(i => (i.Key, InstanceConnection.Create(ConnectAsync, i.Value))).ToDictionary();

        connectionManager = Task.Run(async () =>
        {
            foreach (var item in instanceConnections)
                await item.Value.Start();
        }); // bandaid
    }

    public async Task AddInstance(InstanceInvite inviteCode)
    {
        Guid guid = Guid.NewGuid();
        (InstanceConnection connection, InstanceRegistration registration, HashedId? referrer) = await InstanceConnection.Create(ConnectAsync, inviteCode, persistentState.Profile);
        persistentState.AddInstance(guid, registration);
        if (referrer.HasValue)
            persistentState.AddRecipient(registration.InstanceAddress, referrer.Value);
        instanceConnections.Add(guid, connection);
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

        // use a TaskCompletionSource; on any transaction: return & cancel others
        // write to ledger. (here or by consumer?)
        // this is very similar to the way transceivers function with gateways. some potential for abstraction here (is it worth it?)
    }
    
    

    public async Task<InstanceInvite> RequestInviteCode(Guid instance)
    {
        InstanceConnection instanceConnection = instanceConnections[instance];
        Guid code = await instanceConnection.RequestInviteCode();
        return new(instanceConnection.Registration.InstanceAddress, code);
    }
}