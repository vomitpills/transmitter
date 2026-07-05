namespace Transmitter.IO;

partial class AppendOnlyStorage<T>
{
    private void ReadAndValidateStorageMetadata()
    {
        lock (mutex)
        {
            reader.BaseStream.Position = 0;
            ushort readVersion = reader.ReadUInt16();
            if (VERSION != readVersion)
                throw new InvalidDataException($"Version mismatch: expected '{VERSION}', got '{readVersion}'");
        }
    }

    private void WriteStorageMetadata()
    {
        lock (mutex)
        {
            writer.Write(VERSION);
        }
    }

    public void Append(T item)
    {
        lock (mutex)
        {
            long written = writer.BaseStream.Position;
            item.Serialize(writer);
            written = writer.BaseStream.Position - written; // slop
            writer.Write((ushort)written);
            writer.Flush();
        }
    }

    private Entry GetLast()
    {
        lock (mutex)
        {
            reader.BaseStream.Seek(0, SeekOrigin.End);
            if (!TryReadEntryData(StreamPositionAnchor.AfterLength, out Entry entry))
                throw new InvalidDataException();
            return entry;
        }
    }

    private bool TryGetPreviousEntry(Entry entry, out Entry previous)
    {
        lock (mutex)
        {
            reader.BaseStream.Position = entry.Position;
            return TryReadEntryData(StreamPositionAnchor.AfterLength, out previous);
        }
    }

    private Entry GetPreviousEntry(Entry entry)
    {
        lock (mutex)
        {
            if (!TryGetPreviousEntry(entry, out Entry previous))
                throw new InvalidDataException();
            return previous;
        }
    }

    private bool TryReadEntryData(StreamPositionAnchor anchor, out Entry entry)
    {
        lock (mutex)
        {
            if (anchor is not StreamPositionAnchor.BeforeLength and not StreamPositionAnchor.AfterLength)
                throw new ArgumentOutOfRangeException(nameof(anchor), $"'{anchor}' is undefined for {nameof(StreamPositionAnchor)}");

            long position = reader.BaseStream.Position;
            if (anchor is StreamPositionAnchor.AfterLength)
                position -= sizeof(ushort);

            if (position <= METADATA_LENGTH)
            {
                entry = default;
                return false;
            }
            reader.BaseStream.Position = position;

            long lengthPosition = reader.BaseStream.Position;
            ushort length =  reader.ReadUInt16();
            entry = new(this, lengthPosition - length, length);
            return true;
        }
    }

    private T DeserializeEntry(Entry entry)
    {
        lock (mutex)
        {
            reader.BaseStream.Position = entry.Position;
            return T.Deserialize(reader); // Uh-oh, no bounds check!
        }
    }

    public interface IEntry : IEquatable<IEntry>
    {
        public long Position { get; }
        public ushort Length { get; }
        public Lazy<T> Value { get; }
    }

    private readonly struct Entry : IEntry
    {
        public long Position { get; }
        public ushort Length { get; }
        public Lazy<T> Value { get; }

        public Entry(AppendOnlyStorage<T> owner, long position, ushort length)
        {
            Position = position;
            Length = length;

            Entry @this = this; // not sure abt this one
            Value = new(() => owner.DeserializeEntry(@this));
        }

        public bool Equals(IEntry? other) => other is not null && Position == other.Position && Length == other.Length;

        public static bool operator ==(Entry a, IEntry b) => a.Equals(b);
        public static bool operator !=(Entry a, IEntry b) => !a.Equals(b);

        public override bool Equals(object? obj) => obj is IEntry entry && Equals(entry);

        public override int GetHashCode() => HashCode.Combine(Position, Length);
    }

    private enum StreamPositionAnchor
    {
        BeforeLength, AfterLength
    }
}
