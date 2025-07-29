using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinterRose.WinterForgeSerializing.Formatting;

internal class TextLineStream : Stream
{
    private readonly BlockingCollection<string> _lines = new();
    private readonly Encoding _encoding = Encoding.UTF8;
    private bool _isCompleted = false;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public void WriteLine(string line)
    {
        if (_isCompleted) throw new InvalidOperationException("Cannot write to a completed stream.");
        _lines.Add(line);
    }

    public string? ReadLine()
    {
        try
        {
            return _lines.Take();
        }
        catch (InvalidOperationException)
        {
            return null; // collection was marked complete
        }
    }

    public void Complete()
    {
        if (!_isCompleted)
        {
            _isCompleted = true;
            _lines.CompleteAdding();
        }
    }

    public override void Flush()
    {
        Complete();
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (_isCompleted) return;

        string text = _encoding.GetString(buffer, offset, count);
        using var reader = new StringReader(text);
        string? line;
        while ((line = reader.ReadLine()) != null)
            _lines.Add(line);
    }
}

