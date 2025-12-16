using System;
using System.Buffers;
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
        if (!CacheStream.CanWrite)
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
    private readonly object sync = new();
    private long _position = 0; 

    private const int COPY_BUFFER_SIZE = 8192;

    public DualStreamReader(Stream firstStream, Stream secondStream)
    {
        this.firstStream = firstStream ?? throw new ArgumentNullException(nameof(firstStream));
        this.secondStream = secondStream ?? throw new ArgumentNullException(nameof(secondStream));

        if (!firstStream.CanRead || !firstStream.CanSeek || !firstStream.CanWrite)
            throw new ArgumentException("First stream (cache) must support Read, Seek and Write.", nameof(firstStream));
        if (!secondStream.CanRead)
            throw new ArgumentException("Second stream must be readable", nameof(secondStream));
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;

    public override long Length
    {
        get
        {
            lock (sync)
            {
                if (!secondStream.CanSeek)
                    throw new NotSupportedException("Length is not available because the underlying source stream is not seekable.");
                return firstStream.Length + (secondStream.Length - secondStream.Position);
            }
        }
    }

    public override long Position
    {
        get
        {
            lock (sync) { return _position; }
        }
        set
        {
            Seek(value, SeekOrigin.Begin);
        }
    }

    public override void Flush() { /* no-op */ }

    private long EnsureCached(long required)
    {
        lock (sync)
        {
            if (required <= firstStream.Length)
                return firstStream.Length;

            if (!secondStream.CanRead)
                return firstStream.Length;

            Span<byte> buffer = stackalloc byte[COPY_BUFFER_SIZE];

            while (firstStream.Length < required)
            {
                int toRead = (int)Math.Min(COPY_BUFFER_SIZE, required - firstStream.Length);
                int read;
                // Use synchronous Read on second stream (we are inside lock)
                read = secondStream.Read(buffer[..Math.Max(1, toRead)]);
                if (read <= 0)
                    break; // EOF reached on second stream

                // Append into cache (firstStream)
                long prevPos = firstStream.Position;
                firstStream.Position = firstStream.Length; // append
                firstStream.Write(buffer[..read]);
                firstStream.Flush();
                firstStream.Position = prevPos; // restore first stream position if somebody relies on it
            }

            return firstStream.Length;
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (buffer is null) throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || count < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException();

        lock (sync)
        {
            // Ensure requested bytes are cached (or as many as possible until EOF)
            long wantedEnd = _position + count;
            EnsureCached(wantedEnd);

            // Position firstStream to logical _position and read
            firstStream.Position = _position;
            int read = firstStream.Read(buffer, offset, count);
            _position += read;
            return read;
        }
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (buffer is null) throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || count < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException();

        // Best-effort: perform the caching step synchronously within a lock to avoid races,
        // then perform the actual read from the cached stream asynchronously.
        // This keeps semantics simple and avoids subtle interleavings.
        long wantedEnd;
        lock (sync)
        {
            wantedEnd = _position + count;
            EnsureCached(wantedEnd);
            firstStream.Position = _position;
        }

        int read = await firstStream.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);

        lock (sync) { _position += read; }
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        lock (sync)
        {
            long target;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    target = offset;
                    break;
                case SeekOrigin.Current:
                    target = _position + offset;
                    break;
                case SeekOrigin.End:
                    if (!secondStream.CanSeek)
                        throw new NotSupportedException("SeekOrigin.End is not supported when the underlying source stream is not seekable.");
                    // compute total length and apply offset
                    long totalLength = firstStream.Length + (secondStream.Length - secondStream.Position);
                    target = totalLength + offset;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(origin));
            }

            if (target < 0) throw new IOException("Attempted to seek before begin of stream.");

            // Ensure the cache contains the requested position (read from secondStream if needed)
            EnsureCached(target);

            // If requested position is beyond cached & secondStream is at EOF, Seek beyond EOF is not allowed
            if (target > firstStream.Length)
                throw new IOException("Attempted to seek past end of stream.");

            _position = target;
            return _position;
        }
    }

    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override int Read(Span<byte> buffer)
    {
        lock (sync)
        {
            long wantedEnd = _position + buffer.Length;
            EnsureCached(wantedEnd);

            firstStream.Position = _position;
            int read = firstStream.Read(buffer);
            _position += read;
            return read;
        }
    }
}
