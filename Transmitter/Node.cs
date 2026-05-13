using System.Net;
using System.Text;

namespace Transmitter;

public sealed class Node : Transceiver
{
    private readonly List<(IPEndPoint, Guid)> instances = [];

    public override Logger Logger => new(nameof(Node));

    public async Task<bool> AddInstance(IPEndPoint endPoint)
    {
        if (await SendRequest(new RegisterRequest(), endPoint) is not GuidResponse response)
            throw new InvalidOperationException("Failed to register");

        Console.WriteLine($"Received GUID {response.Guid}");
        return response.Status is RequestStatus.OK;
    }
}