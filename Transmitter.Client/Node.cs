using System.Collections.ObjectModel;

using Transmitter.Comms;
using Transmitter.Comms.Gateways;
using Transmitter.Security;

namespace Transmitter.Client;

public abstract class Node : Transceiver
{
    public Dictionary<Guid, InstanceRegistration> Instances { get; } = []; // mutable
    protected abstract Dictionary<Guid, InstanceConnection> InstanceConnections { get; }

    public async Task<InstanceConnection> AddInstance(InstanceInvite inviteCode, Profile profile)
    {
        Guid guid = Guid.NewGuid();
        (InstanceConnection connection, InstanceRegistration registration, HashedId? referrer) =
            await InstanceConnection.Create(ConnectAsync, inviteCode, profile);
        Instances.Add(guid, registration);
        // if (referrer.HasValue)
        //     persistentState.AddRecipient(registration.InstanceAddress, referrer.Value);
        return connection;
    }

    public async Task<InstanceInvite> RequestInviteCode(Guid instance)
    {
        InstanceConnection instanceConnection = InstanceConnections[instance];
        Guid code = await instanceConnection.RequestInviteCode();
        return new(instanceConnection.Registration.InstanceAddress, code);
    }

    protected async Task<TransactionReceipt> Relay(Request request, Recipient recipient, CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource transactionTokenSource = new();
        using CancellationTokenSource linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(transactionTokenSource.Token, cancellationToken);
        List<Task<TransactionReceipt>> tasks = [];
        foreach (var item in recipient.Contacts.OrderBy((a) => Random.Shared.Next()).Take(5))
            tasks.Add(Task.Run(() => Relay(request, item, linkedTokenSource.Token)));
        return await await Task.WhenAny(tasks);
    }

    private async Task<TransactionReceipt> Relay(Request request, RecipientContact contact, CancellationToken cancellationToken = default)
    {
        InstanceConnection instanceConnection = InstanceConnections.Values.Single(c => c.Registration.InstanceAddress == contact.InstanceAddress);
        Transaction transaction = Transaction.FromSerializable(contact.Id, request);
        TaskCompletionSource<TransactionReceipt> transactionFinishedTask = new();

        void Callback(TransactionReceipt receipt)
        {
            if (receipt.Transaction.Id != transaction.Id)
                return;
            instanceConnection.TransactionFinished -= Callback;
            transactionFinishedTask.TrySetResult(receipt);
        }

        cancellationToken.Register(() =>
        {
            instanceConnection.TransactionFinished -= Callback;
            transactionFinishedTask.SetCanceled(cancellationToken);
        });

        instanceConnection.TransactionFinished += Callback;

        Guid transactionId = await instanceConnection.Relay(transaction);
        cancellationToken.ThrowIfCancellationRequested(); // bandaid; catch and cancel request
        return await transactionFinishedTask.Task;
    }
    // use a TaskCompletionSource; on any transaction: return & cancel others
    // write to ledger. (here or by consumer?)
    // this is very similar to the way transceivers function with gateways. some potential for abstraction here (is it worth it?)
}

public enum TransceiverRole : byte
{
    Node, Relay
}