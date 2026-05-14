using System.Net;

using Transmitter.Comms.Gateways;

namespace Transmitter;

public abstract class Node : Transceiver
{
    private readonly List<(IPEndPoint, Guid)> instances = [];

    public async Task<bool> AddInstance(IPEndPoint endPoint)
    {
        if (await SendRequest(new RegisterRequest(), endPoint) is not GuidResponse response)
            throw new InvalidOperationException("Failed to register");

        Console.WriteLine($"Received GUID {response.Guid}");
        return response.Status is RequestStatus.OK;
    }
}