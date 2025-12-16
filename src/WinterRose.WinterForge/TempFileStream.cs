using System;
using System.Collections.Generic;
using System.Text;

namespace WinterRose.WinterForgeSerializing;

public class TempFileStream : Stream
{
    private readonly FileStream file;

    public TempFileStream()
    {
        file = new FileStream(
            Path.GetTempFileName(),
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None,
            4096,
            FileOptions.DeleteOnClose
        );
    }

    public TempFileStream(string path)
    {
        file = new FileStream(
            path,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None,
            4096,
            FileOptions.DeleteOnClose
        );
    }

    public TempFileStream(FileStream file)
    {
        this.file = file;
    }

    public TempFileStream(Stream s) : this()
    {
        s.CopyTo(file);
        file.Position = 0;
    }

    public override bool CanRead => file.CanRead;
    public override bool CanSeek => file.CanSeek;
    public override bool CanWrite => file.CanWrite;

    public override long Length => file.Length;

    public override long Position
    {
        get => file.Position;
        set => file.Position = value;
    }

    public override void Flush() => file.Flush();

    public override int Read(byte[] buffer, int offset, int count) =>
        file.Read(buffer, offset, count);

    public override long Seek(long offset, SeekOrigin origin) =>
        file.Seek(offset, origin);

    public override void SetLength(long value) =>
        file.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count) =>
        file.Write(buffer, offset, count);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            file.Dispose();

        base.Dispose(disposing);
    }
}
