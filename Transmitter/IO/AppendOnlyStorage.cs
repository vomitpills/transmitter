using System.Collections;

namespace Transmitter.IO;

public partial class AppendOnlyStorage<T> : IDisposable, IEnumerable<Lazy<T>> where T : ISerializable<T>
{
    private const ushort VERSION = 1;

    private const int METADATA_LENGTH = sizeof(ushort);

    private readonly BinaryWriter writer;
    private readonly BinaryReader reader;
    private readonly bool closeOnDisposal;
    private readonly Lock mutex = new();

    private readonly SortedList<int, Entry> cache = [];

    public AppendOnlyStorage(string filePath)
    {
        bool isFileSetUp = File.Exists(filePath); // should be the consumer's responsibility (?), make one ctor using Stream
        FileStream writeStream = new(filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
        FileStream readStream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Write);
        writer = new(writeStream, System.Text.Encoding.Default, false);
        reader = new(readStream, System.Text.Encoding.Default, false);
        closeOnDisposal = true;

        if (!isFileSetUp)
            WriteStorageMetadata();
        else
            ReadAndValidateStorageMetadata();
    }

    public T GetItemtAt(int index)
    {
        Entry entry = FetchEntryAt(index);
        return DeserializeEntry(entry);
    }

    private Entry FetchEntryAt(int index)
    {
        EnsureCacheNotEmpty();
        if (!cache.TryGetValue(index, out Entry entry))
        {
            int start = GetNearestAnchor(index);
            entry = cache[start];
            for (int i = 0; i < start - index; i++)
                entry = GetPreviousEntry(entry);
            cache[index] = entry;
        }
        return entry;
    }

    private int FetchPosition(Entry entry)
    {
        EnsureCacheNotEmpty();
        int anchorPos = GetNearestAnchor(entry);
        Entry anchor = FetchEntryAt(anchorPos);
        do
        {
            if (anchor == entry)
            {
                cache[anchorPos] = entry;
                return anchorPos;
            }
            anchorPos--;
        } while (TryGetPreviousEntry(anchor, out anchor));
        throw new ArgumentOutOfRangeException(nameof(entry), "Could not locate provided entry");
    }

    private int GetNearestAnchor(int index)
    {
        EnsureCacheNotEmpty();
        return cache.Keys.Where(i => i > index).Min();
    }

    private int GetNearestAnchor(Entry entry) // 2-way dictionary is needed
    {
        EnsureCacheNotEmpty();
        return cache.Where(pair => pair.Value.Position > entry.Position).Min(pair => pair.Key);
    }

    private void EnsureCacheNotEmpty()
    {
        if (cache.Count > 0)
            return;

        Entry anchor = GetLast();
        Entry pointer = anchor;
        int i = 0;
        for (; TryGetPreviousEntry(pointer, out pointer); i++) { }
        cache[i] = anchor;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (closeOnDisposal)
        {
            writer.Dispose();
            reader.Dispose();
        }
    }

    public IEnumerator<Lazy<T>> GetEnumerator()
    {
        Entry entry = GetLast();
        do
        {
            yield return entry.Value;
        } while (TryGetPreviousEntry(entry, out entry));
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}