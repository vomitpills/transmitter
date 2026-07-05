using Transmitter.Comms;
using Transmitter.Comms.Gateways;

namespace Transmitter.Client;

public abstract class Node : Transceiver
{
    protected Task<Response> Request(Request request, Recipient recipient)
    {
        TaskCompletionSource<Response> taskCompletionSource = new();

        return taskCompletionSource.Task;
    }

    private async Task Relay(Request request, Recipient recipient)
    {
        RecipientContact contact = recipient.Contacts.OrderBy((a) => Random.Shared.Next()).First();
        InstanceConnection instanceConnection = instanceConnections.Values.Single(c => c.Registration.InstanceAddress == contact.InstanceAddress);
        await instanceConnection.Relay(request, contact.Passcode);
    }
}

public enum TransceiverRole : byte
{
    Node, Relay
}