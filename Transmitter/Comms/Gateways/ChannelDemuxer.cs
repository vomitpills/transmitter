using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Transmitter.Comms.Gateways;

internal class ChannelDemuxer<TKey, TValue>(int channelCapacity = 16) where TKey : notnull
{
    private readonly InnerDictionary innerDictionary = new(channelCapacity);

    public ValueTask WriteAsync(TKey key, TValue value, CancellationToken cancellationToken = default)
    {
        Channel<TValue> channel = innerDictionary.RetrieveChannel(key);
        return channel.Writer.WriteAsync(value, cancellationToken);
    }

    public ValueTask<TValue> ReadAsync(TKey key, CancellationToken cancellationToken = default)
    {
        Channel<TValue> channel = innerDictionary.RetrieveChannel(key);
        return channel.Reader.ReadAsync(cancellationToken);
    }

    private sealed class InnerDictionary(int channelCapacity) // bandaid
    {
        private readonly ConcurrentDictionary<TKey, Channel<TValue>> innerChannelDictionary = [];
        public Channel<TValue> RetrieveChannel(TKey key) => innerChannelDictionary.GetOrAdd(key, (_) => Channel.CreateBounded<TValue>(channelCapacity));
    }
}
