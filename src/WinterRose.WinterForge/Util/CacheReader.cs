using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WinterRose.NetworkServer;

public class CacheReader : Stream
{
    public Stream sourceStream { get; }

    public Stream CacheStream { get; }

    public CacheReader(Stream sourceStream, Stream cacheStream)
    {
        this.sourceStream = sourceStream ?? throw new ArgumentNullException(nameof(sourceStream));
        CacheStream = cacheStream ?? throw new ArgumentNullException(nameof(cacheStream));
        if(!CacheStream.CanWrite)
            throw new ArgumentException("CacheStream must be writable", nameof(cacheStream));
    }

    /// <summary>
    /// Creates a new stream that holds the same information of this one.
    /// </summary>
    /// <returns></returns>
    public DualStreamReader CreateFallbackReader()
    {
        CacheStream.Position = 0; // <-- rewind to read everything
        return new DualStreamReader(CacheStream, sourceStream);
    }

    public override bool CanRead => sourceStream.CanRead;
    public override bool CanSeek => sourceStream.CanSeek;
    public override bool CanWrite => sourceStream.CanWrite;
    public override long Length => sourceStream.Length;

    public override long Position
    {
        get => sourceStream.Position;
        set => sourceStream.Position = value;
    }

    public override void Flush() => sourceStream.Flush();
    public void FlushCache() => CacheStream.Flush();

    public override int Read(byte[] buffer, int offset, int count)
    {
        int bytesRead = sourceStream.Read(buffer, offset, count);
        LogBytes(buffer, offset, bytesRead);
        return bytesRead;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        int bytesRead = await sourceStream.ReadAsync(buffer, offset, count, cancellationToken);
        LogBytes(buffer, offset, bytesRead);
        return bytesRead;
    }

    private void LogBytes(byte[] buffer, int offset, int count)
    {
        if (count <= 0) return;
        CacheStream.Write(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin) => sourceStream.Seek(offset, origin);

    public override void SetLength(long value) => sourceStream.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count) => sourceStream.Write(buffer, offset, count);
}

public class DualStreamReader : Stream
{
    private readonly Stream firstStream;
    private readonly Stream secondStream;
    private bool firstStreamExhausted;

    public DualStreamReader(Stream firstStream, Stream secondStream)
    {
        this.firstStream = firstStream ?? throw new ArgumentNullException(nameof(firstStream));
        this.secondStream = secondStream ?? throw new ArgumentNullException(nameof(secondStream));

        if (!firstStream.CanRead)
            throw new ArgumentException("First stream must be readable", nameof(firstStream));
        if (!secondStream.CanRead)
            throw new ArgumentException("Second stream must be readable", nameof(secondStream));
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() { /* no-op */ }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (!firstStreamExhausted)
        {
            int read = firstStream.Read(buffer, offset, count);
            if (read > 0)
                return read;

            // First stream done, switch to second
            firstStreamExhausted = true;
        }

        return secondStream.Read(buffer, offset, count);
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (!firstStreamExhausted)
        {
            int read = await firstStream.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
            if (read > 0)
                return read;

            // First stream done, switch to second
            firstStreamExhausted = true;
        }

        return await secondStream.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
