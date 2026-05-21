using System.Diagnostics;

namespace Transmitter;

internal class LoggedStream(Stream innerStream, Logger logger) : Stream, IDisposable
{
    public override bool CanRead => innerStream.CanRead;

    public override bool CanSeek => innerStream.CanSeek;

    public override bool CanWrite => innerStream.CanWrite;

    public override long Length => innerStream.Length;

    public override long Position { get => innerStream.Position; set => innerStream.Position = value; }

    [DebuggerHidden]
    public override void Flush()
    {
        logger.Log(">> FLUSH");
        innerStream.Flush();
    }

    [DebuggerHidden]
    public override int Read(byte[] buffer, int offset, int count)
    {
        int bytesRead = innerStream.Read(buffer, offset, count);
        logger.Log($"<- READ: {Convert.ToHexString(buffer)}");
        return bytesRead;
    }

    [DebuggerHidden]
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        int bytesRead = await innerStream.ReadAsync(buffer, offset, count, cancellationToken);
        logger.Log($"<- READ: {Convert.ToHexString(buffer)}");
        return bytesRead;
    }

    [DebuggerHidden]
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        int bytesRead = await innerStream.ReadAsync(buffer, cancellationToken);
        logger.Log($"<- READ: {Convert.ToHexString(buffer.Span)}");
        return bytesRead;
    }

    [DebuggerHidden]
    public override long Seek(long offset, SeekOrigin origin)
    {
        return innerStream.Seek(offset, origin);
    }

    [DebuggerHidden]
    public override void SetLength(long value)
    {
        innerStream.SetLength(value);
    }

    [DebuggerHidden]
    public override void Write(byte[] buffer, int offset, int count)
    {
        logger.Log($"-> WRITE: {Convert.ToHexString(buffer)}");
        innerStream.Write(buffer, offset, count);
    }
}