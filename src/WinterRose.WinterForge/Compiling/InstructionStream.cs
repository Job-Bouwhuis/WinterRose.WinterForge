using System.Runtime.ExceptionServices;
using System.Threading.Channels;
using WinterRose.WinterForgeSerializing.Instructions;

namespace WinterRose.WinterForgeSerializing.Compiling;


public class InstructionStream : IReadOnlyList<Instruction>
{
    readonly Channel<Instruction> _channel = Channel.CreateUnbounded<Instruction>();
    readonly List<Instruction> _items = new();
    ExceptionDispatchInfo? _failure;

    // Producer API
    internal void Add(Instruction ins)
    {
        if (!_channel.Writer.TryWrite(ins))
            throw new InvalidOperationException("Failed to write instruction to stream.");
    }

    internal void Complete()
    {
        _channel.Writer.TryComplete(_failure?.SourceException);
    }

    internal void Fail(Exception ex)
    {
        _failure = ExceptionDispatchInfo.Capture(ex);
        _channel.Writer.TryComplete(ex);
    }

    // IReadOnlyList members
    public Instruction this[int index]
    {
        get
        {
            while (true)
            {
                if (index < _items.Count)
                    return _items[index];

                // If a failure happened, throw it
                if (_failure != null)
                    _failure.Throw();

                // Try to read from the channel (blocking)
                if (!_channel.Reader.WaitToReadAsync().AsTask().GetAwaiter().GetResult())
                {
                    // Channel completed and no more items
                    throw new IndexOutOfRangeException("Attempted to read past end of instruction stream.");
                }

                while (_channel.Reader.TryRead(out var ins))
                {
                    _items.Add(ins);
                }
            }
        }
    }


    public int Count
    {
        get
        {
            if (_failure != null)
                _failure.Throw();
            return _channel.Reader.Completion.IsCompleted ? _items.Count : int.MaxValue;
        }
    }

    public IEnumerator<Instruction> GetEnumerator()
    {
        int index = 0;
        while (true)
        {
            Instruction next;
            try
            {
                next = this[index];
            }
            catch (IndexOutOfRangeException)
            {
                yield break;
            }
            yield return next;
            index++;
        }
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    public static implicit operator InstructionStream(List<Instruction> instructions)
    {
        var stream = new InstructionStream();
        foreach (var ins in instructions)
            stream.Add(ins);
        stream.Complete();
        return stream;
    }
}
