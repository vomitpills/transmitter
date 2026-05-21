using System.Text;

using Transmitter.Comms;
using Transmitter.IO;

namespace Transmitter.Client;

partial class PersistentClientState : IDisposable // move shared logic into an abstract class
{
    private const ushort FORMAT_VERSION = 0;

    private readonly AtomicWriter atomicWriter;

    private PersistentClientState(string filePath)
    {
        atomicWriter = new(filePath);
    }

    public static PersistentClientState LoadOrCreate(string filePath)
    {
        PersistentClientState state;
        if (!File.Exists(filePath))
        {
            state = new(filePath);
            state.Save();
        }
        else
            state = Load(filePath);
        return state;
    }

    private void Save()
    {
        using BinaryWriter writer = new(atomicWriter.BeginTransaction());
        writer.Write(BitConverter.GetBytes(FORMAT_VERSION));
        
    }

    private static PersistentClientState Load(string filePath)
    {
        using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
        using BinaryReader reader = new(stream, Encoding.Default, true);

        if (reader.ReadUInt16() != FORMAT_VERSION)
            throw new InvalidOperationException("Format version mismatch");

        Profile? profile = reader.ReadBoolean()? Profile.Deserialize(reader) : null;

        return new(filePath)
        {
            Profile = profile
        };
    }

    public void Dispose()
    {
        atomicWriter.Dispose();
    }
}
