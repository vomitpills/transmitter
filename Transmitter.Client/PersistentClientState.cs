using Transmitter.Comms;

namespace Transmitter.Client;

public sealed partial class PersistentClientState
{
    private Profile? Profile { get; init; }
    private Dictionary<Guid, (Profile, DateTimeOffset)> ProfileCache { get; init; } = [];
}
