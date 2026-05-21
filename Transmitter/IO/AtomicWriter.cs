namespace Transmitter.IO;

public class AtomicWriter(string filePath) : IDisposable
{
    private readonly SemaphoreSlim writeSemaphore = new(1);
    private readonly List<AtomicWriterStream> activeTransactions = [];

    public Stream BeginTransaction()
    {
        string tempFilePath = filePath + Random.Shared.GetHexString(8);
        AtomicWriterStream stream = new(this, writeSemaphore, tempFilePath);
        activeTransactions.Add(stream);
        return stream;
    }

    private void FinalizeTransaction(AtomicWriterStream carrier, string copy)
    {
        if (File.Exists(filePath))
            File.Replace(copy, filePath, null);
        else
            File.Move(copy, filePath);
        activeTransactions.Remove(carrier);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        foreach (var item in activeTransactions)
            item.Dispose();
    }

    private class AtomicWriterStream : Stream
    {
        private readonly AtomicWriter owner;
        private readonly string tempFilePath;
        private readonly Stream tempStream;
        private readonly SemaphoreSlim semaphore;

        public AtomicWriterStream(AtomicWriter owner, SemaphoreSlim semaphore, string tempFilePath)
        {
            this.semaphore = semaphore;
            semaphore.Wait();
            try
            {
                this.owner = owner;
                this.tempFilePath = tempFilePath;
                tempStream = new FileStream(tempFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            }
            catch
            {
                semaphore.Release();
                throw;
            }
        }

        public override bool CanRead => tempStream.CanRead;
        public override bool CanSeek => tempStream.CanSeek;
        public override bool CanWrite => tempStream.CanWrite;
        public override long Length => tempStream.Length;
        public override long Position { get => tempStream.Position; set => tempStream.Position = value; }
        public override void Flush() => tempStream.Flush();
        public override int Read(byte[] buffer, int offset, int count) => tempStream.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => tempStream.Seek(offset, origin);
        public override void SetLength(long value) => tempStream.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => tempStream.Write(buffer, offset, count);

        private bool _disposed = false;
        protected override void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                tempStream.Dispose();
                owner.FinalizeTransaction(this, tempFilePath);
                semaphore.Release();
            }

            base.Dispose(disposing);
            _disposed = true;
        }
    }
}