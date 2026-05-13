namespace Transmitter;

public sealed class Instance() : Transceiver
{
    

    //[RateLimited(TimeSpan.FromSeconds(30))]
    private static Guid GetIdentity()
    {
        Guid nodeGuid = Guid.NewGuid();
        return nodeGuid;
    }
}