using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WinterRose.Reflection;
using WinterRose.WinterForgeSerializing.Instructions;
using WinterRose.WinterForgeSerializing.Workers;

namespace WinterRose.WinterForgeSerializing.Compiling;

public enum ValuePrefix : byte
{
    NONE = 0x00,
    STRING = 0x01,
    INT = 0x02,
    REF = 0x04,
    STACK = 0x05,
    DEFAULT = 0x06,
    BOOL = 0x07,
    MULTILINE_STRING = 0x08,
    FLOAT = 0x09,
    SHORT = 0x0A,
    USHORT = 0x0B,
    UINT = 0x0C,
    LONG = 0x0D,
    ULONG = 0x0E,
    DOUBLE = 0x0F,
    BYTE = 0x10,
    SBYTE = 0x11,
    CHAR = 0x12,
    DECIMAL = 0x13,
    NULL = 0x14
}



public class OpcodeToByteCompiler : Stream
{
    private Stack<Type> instanceStack = new Stack<Type>();
    List<string>? bufferedObject = null;
    Type? bufferedType = null;
    private ICustomValueCompiler customCompiler;
    private int bufferedObjectRefID;
    private int nesting;
    private bool allowCustomCompilers = true;
    private bool allowedRehydrate = true;
    private bool SeenEndOfDataMark = false;
    private int _maxParallelismDefault = Math.Max(1, Environment.ProcessorCount);

    private readonly Decoder decoder = Encoding.UTF8.GetDecoder();
    private readonly StringBuilder writeBuffer = new();
    private readonly System.Collections.Concurrent.BlockingCollection<string> lineQueue = new();
    private readonly Task? backgroundProcessingTask;
    private readonly CancellationTokenSource cancellation = new();
    private readonly Stream? bytesDestinationStream;

    private readonly ObjectPool<MemoryStream> memoryStreamPool = new(resetAction: ms => ms.SetLength(0));

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    private string? peekedLine = null;

    private string? Peek(StreamReader reader)
    {
        peekedLine ??= reader.ReadLine();
        return peekedLine;
    }

    public override void Flush()
    {
        // no-op — we flush to destination in the background consumer
    }

    private string? ReadLine(StreamReader reader)
    {
        if (peekedLine != null)
        {
            string line = peekedLine;
            peekedLine = null;
            return line;
        }
        return reader.ReadLine();
    }

    private void Consume(StreamReader reader, int count)
    {
        // If we had a peeked line waiting, consume it first if needed
        if (peekedLine != null)
        {
            peekedLine = null;
            count--;
        }

        // Read and discard the remaining lines
        for (int i = 0; i < count; i++)
        {
            if (reader.ReadLine() == null)
                break; // End of stream reached early
        }
    }


    internal OpcodeToByteCompiler(Stream bytesDestination, bool allowCustomCompilers = true)
    {
        this.allowCustomCompilers = allowCustomCompilers;
        this.bytesDestinationStream = bytesDestination ?? throw new ArgumentNullException(nameof(bytesDestination));
        backgroundProcessingTask = Task.Run(() => BackgroundProcessLinesAsync(cancellation.Token));
    }

    private ValuePrefix GetNumericValuePrefix(Type value)
    {
        if (value == null)
            return ValuePrefix.NONE;

        return value switch
        {
            _ when value == typeof(byte) => ValuePrefix.BYTE,
            _ when value == typeof(sbyte) => ValuePrefix.SBYTE,
            _ when value == typeof(short) => ValuePrefix.SHORT,
            _ when value == typeof(ushort) => ValuePrefix.USHORT,
            _ when value == typeof(int) => ValuePrefix.INT,
            _ when value == typeof(uint) => ValuePrefix.UINT,
            _ when value == typeof(long) => ValuePrefix.LONG,
            _ when value == typeof(ulong) => ValuePrefix.ULONG,
            _ when value == typeof(float) => ValuePrefix.FLOAT,
            _ when value == typeof(double) => ValuePrefix.DOUBLE,
            _ when value == typeof(decimal) => ValuePrefix.DECIMAL,
            _ when value == typeof(char) => ValuePrefix.CHAR,
            _ => ValuePrefix.NONE,
        };

    }

    object Rehydrate(List<string> lines, Type type)
    {
        using var stream = memoryStreamPool.Using();
        using var writer = new StreamWriter(stream);
        foreach (string l in lines)
            writer.WriteLine(l);
        writer.WriteLine($"8 {lines[^1].Split(' ')[^1]}");
        writer.Flush();
        stream.Item.Position = 0;

        List<Instruction> instructions = InstructionParser.ParseOpcodes(stream);
        return WinterForge.DeserializeFromInstructions(instructions);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || count < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException();

        // decode bytes into chars and append to writeBuffer
        int charCount = decoder.GetCharCount(buffer, offset, count);
        char[] chars = new char[charCount];
        decoder.GetChars(buffer, offset, count, chars, 0);
        writeBuffer.Append(chars);

        // extract full lines (support \n and \r\n)
        for (; ; )
        {
            int newlineIdx = IndexOf(writeBuffer, "\n");
            if (newlineIdx < 0) break;

            // capture the line (strip trailing '\r' if present)
            string fullLine = writeBuffer.ToString(0, newlineIdx + 1);
            string line = fullLine.EndsWith("\r\n") ? fullLine[..^2] : fullLine.EndsWith("\n") ? fullLine[..^1] : fullLine;
            line = line.TrimEnd('\r', '\n');

            // remove consumed part from buffer
            writeBuffer.Remove(0, newlineIdx + 1);

            // enqueue the line for processing
            lineQueue.Add(line);
        }
    }

    public static int IndexOf(StringBuilder sb, string value)
    {
        if (sb == null || value == null)
            throw new ArgumentNullException();

        if (value.Length == 0)
            return 0; // empty string is always at index 0

        for (int i = 0; i <= sb.Length - value.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < value.Length; j++)
            {
                if (sb[i + j] != value[j])
                {
                    match = false;
                    break;
                }
            }
            if (match)
                return i;
        }
        return -1; // not found
    }


    public void Compile(Stream textOpcodes, Stream bytesDestination)
    {
        // keep the synchronous API but run the async pipeline under the hood
        CompileAsync(textOpcodes, bytesDestination, _maxParallelismDefault).GetAwaiter().GetResult();
    }

    public async Task CompileAsync(Stream textOpcodes, Stream bytesDestination, int maxDegreeOfParallelism = 0)
    {
        if (maxDegreeOfParallelism <= 0)
            maxDegreeOfParallelism = _maxParallelismDefault;

        // queue of tasks producing byte[] in the exact order they were enqueued
        var queue = new System.Collections.Concurrent.BlockingCollection<Task<byte[]>>();

        // consumer: writes tasks' results to final output in the same order tasks were added
        var consumer = Task.Run(async () =>
        {
            using var finalWriter = new BinaryWriter(bytesDestination, Encoding.UTF8, leaveOpen: true);
            try
            {
                foreach (var task in queue.GetConsumingEnumerable())
                {
                    var bytes = await task.ConfigureAwait(false);
                    if (bytes?.Length > 0)
                        finalWriter.Write(bytes);
                }

                // If target is a NetworkStream and no explicit WF_ENDOFDATA was seen during parsing,
                // original behavior appended an END_OF_DATA byte at the end. Preserve that here.
                if (bytesDestination is NetworkStream && !SeenEndOfDataMark)
                {
                    finalWriter.Write((byte)OpCode.END_OF_DATA);
                }

                finalWriter.Flush();
            }
            catch
            {
                // ensure we don't swallow exceptions — rethrow after disposing writer (consumer task will fault)
                throw;
            }
        });

        // producer: parse input, create immediate byte[] tasks or background compile tasks and enqueue them
        using var reader = new StreamReader(textOpcodes, leaveOpen: true);

        string? line;
        bool bufferingActive = bufferedObject != null; // not used for this path but preserve logic
                                                       // order is implicit in queue ordering; BlockingCollection preserves insertion order
        while ((line = ReadLine(reader)) != null)
        {
            // Quick handling for WF_ENDOFDATA to ensure exact behavior and ordering
            if (line == "WF_ENDOFDATA")
            {
                SeenEndOfDataMark = true;
                queue.Add(Task.FromResult(new byte[] { (byte)OpCode.END_OF_DATA }));
                break;
            }

            if (!ValidateLine(line, out var parts, out var opcodeByte))
            {
                // ignore whitespace/comments; ValidateLine already sets SeenEndOfDataMark where appropriate
                continue;
            }

            var opcode = (OpCode)opcodeByte;

            // Special-case DEFINE: check for custom compiler — if present, buffer the define block and
            // offload rehydrate+compile to a Task; enqueue the Task so consumer will write it at the correct place.
            if (opcode is OpCode.DEFINE)
            {
                Type t = WinterForgeVM.ResolveType(parts[1]);

                if (allowCustomCompilers && CustomValueCompilerRegistry.TryGetByType(t, out ICustomValueCompiler foundCompiler))
                {
                    // start buffering the DEFINE block (first line stored)
                    List<string> localBuffered = [line];
                    int localNesting = 1;

                    // read lines until nesting ends (matching original ParseOptimizedCompile behavior)
                    while (localNesting > 0 && (line = ReadLine(reader)) != null)
                    {
                        // keep lines verbatim (we will reparse them in Rehydrate)
                        if (line.StartsWith(((byte)OpCode.END).ToString() + " "))
                            localNesting--;

                        localBuffered.Add(line);
                    }

                    // capture relevant values for the Task
                    int localRefId = int.Parse(parts[2]); // object id
                    Type localType = t;
                    var localCompiler = foundCompiler;

                    // create background task that rehydrates and compiles this object to bytes
                    Task<byte[]> compileTask = Task.Run(() =>
                    {
                        // rehydrate instance from buffered text opcodes
                        object instance = Rehydrate(localBuffered, localType);

                        // compile into a memory stream (same output format original used)
                        using var ms = memoryStreamPool.Using();
                        using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

                        // header written by original code
                        bw.Write((byte)0x00);
                        bw.Write((byte)0x00);
                        bw.Write(localCompiler.CompilerId);
                        bw.Write(localRefId);

                        // custom compiler writes bytes representing the instance
                        localCompiler.Compile(bw, instance);
                        bw.Flush();
                        return ms.Item.ToArray();
                    });

                    queue.Add(compileTask);
                    continue; // proceed to next input line
                }
            }

            // Non-custom or DEFINE-without-custom path:
            // Emit opcode to a temporary memory stream (so we can get its bytes), then enqueue an immediate Task
            using (var ms = memoryStreamPool.Using())
            {
                using (var tempWriter = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
                {
                    // reuse existing EmitOpcode logic which writes to BinaryWriter
                    EmitOpcode(tempWriter, line, parts, opcodeByte, opcode);
                    tempWriter.Flush();
                }

                byte[] bytes = ms.Item.ToArray();
                queue.Add(Task.FromResult(bytes));
            }
        }

        // finished producing items — signal consumer and await it
        queue.CompleteAdding();
        await consumer.ConfigureAwait(false);
    }


    private void ParseOptimizedCompile(BinaryWriter writer, string line, ref byte opcodeByte, OpCode opcode, ref bool bufferingActive)
    {
        if (opcode is not OpCode.SET and not OpCode.END)
        {
            // Illegal opcode detected for optimized rehydration

            allowCustomCompilers = false;

            foreach (string bufferedLine in bufferedObject)
            {
                ValidateLine(bufferedLine, out var bufferedParts, out var bufferedOpcodeByte);
                EmitOpcode(writer, bufferedLine, bufferedParts, bufferedOpcodeByte, (OpCode)bufferedOpcodeByte);
            }

            // this loose scope is intentional to not interfere with the above foreach loop
            {
                ValidateLine(line, out var bufferedParts, out var bufferedOpcodeByte);
                EmitOpcode(writer, line, bufferedParts, bufferedOpcodeByte, (OpCode)bufferedOpcodeByte);
            }
            allowCustomCompilers = true;

            bufferingActive = false;

            // Clear buffered state since we're done flushing
            bufferedObject = null;
            bufferedType = null;
            bufferedObjectRefID = -1;
            allowedRehydrate = true;

            return;
        }

        // Normal buffering logic if opcode allowed
        if (line.StartsWith(((byte)OpCode.END).ToString() + " "))
            nesting--;

        bufferedObject.Add(line);

        if (nesting == 0)
        {
            if (allowedRehydrate)
            {
                object instance = Rehydrate(bufferedObject, bufferedType!);

                writer.Write((byte)0x00);
                writer.Write((byte)0x00);
                writer.Write(customCompiler.CompilerId);
                writer.Write(bufferedObjectRefID);
                customCompiler!.Compile(writer, instance);
            }
            else
            {
                throw new UnreachableException("if you did anyway. get this info to the author via discord 'thesnowowl':\n\n" +
                    $"{nameof(OpcodeToByteCompiler)}.{nameof(ParseOptimizedCompile)}-impossible else statement reached.\n\nMany thanks in advance!");
            }

            bufferedObject = null;
            bufferedType = null;
            bufferedObjectRefID = -1;
            allowedRehydrate = true;

            bufferingActive = false; // end of object, stop buffering
        }
    }

    private bool ValidateLine(string line, out string[] parts, out byte opcodeByte)
    {
        parts = Array.Empty<string>();
        opcodeByte = 0;

        if (string.IsNullOrWhiteSpace(line))
            return false;

        if (line.StartsWith("//")) // skip comments
            return false;

        if (line == "WF_ENDOFDATA")
        {
            SeenEndOfDataMark = true;
            return false;
        }

        parts = SplitOpcodeLine(line.Trim());
        if (parts.Length == 0)
            return false;

        if (!byte.TryParse(parts[0], out opcodeByte) || !Enum.IsDefined(typeof(OpCode), opcodeByte))
            throw new InvalidOperationException($"Invalid opcode: {parts[0]}");

        return true;
    }

    private async Task BackgroundProcessLinesAsync(CancellationToken ct)
    {
        if (bytesDestinationStream == null) throw new InvalidOperationException("No bytes destination attached for streaming compile.");

        var byteTaskQueue = new System.Collections.Concurrent.BlockingCollection<Task<byte[]>>();
        var writeTask = Task.Run(async () =>
        {
            using var finalWriter = new BinaryWriter(bytesDestinationStream, Encoding.UTF8, leaveOpen: true);
            try
            {
                foreach (var task in byteTaskQueue.GetConsumingEnumerable(ct))
                {
                    var bytes = await task.ConfigureAwait(false);
                    if (bytes?.Length > 0)
                        finalWriter.Write(bytes);
                }

                if (bytesDestinationStream is System.Net.Sockets.NetworkStream && !SeenEndOfDataMark)
                {
                    finalWriter.Write((byte)OpCode.END_OF_DATA);
                }

                finalWriter.Flush();
            }
            catch
            {
                throw;
            }
        }, ct);

        try
        {
            // Temporary state used when encountering DEFINE with a custom compiler
            List<string>? defineBuffer = null;
            Type? defineType = null;
            int defineNesting = 0;
            int defineRefId = -1;
            ICustomValueCompiler? defineCompiler = null;

            foreach (var rawLine in lineQueue.GetConsumingEnumerable(ct))
            {
                if (ct.IsCancellationRequested) break;

                string line = rawLine;

                // handle explicit WF_ENDOFDATA line written by parser
                if (line == "WF_ENDOFDATA")
                {
                    SeenEndOfDataMark = true;
                    byteTaskQueue.Add(Task.FromResult(new byte[] { (byte)OpCode.END_OF_DATA }));
                    continue;
                }

                if (!ValidateLine(line, out var parts, out var opcodeByte))
                {
                    continue;
                }

                var opcode = (OpCode)opcodeByte;

                // DEFINE + custom compiler path: buffer subsequent lines until matching END found
                if (opcode is OpCode.DEFINE)
                {
                    Type t = WinterForgeVM.ResolveType(parts[1]);

                    if (allowCustomCompilers && CustomValueCompilerRegistry.TryGetByType(t, out ICustomValueCompiler foundCompiler))
                    {
                        // start or continue buffering DEFINE block
                        defineBuffer = new List<string> { line };
                        defineNesting = 1;
                        defineType = t;
                        defineRefId = int.Parse(parts[2]);
                        defineCompiler = foundCompiler;

                        // consume additional lines from the input queue until nesting ends
                        while (defineNesting > 0)
                        {
                            // try to take next line (will throw InvalidOperationException when collection completed)
                            if (!lineQueue.TryTake(out var innerLine, Timeout.Infinite, ct))
                                break;

                            if (innerLine == "WF_ENDOFDATA")
                            {
                                SeenEndOfDataMark = true;
                                // put marker back for main loop to handle if necessary
                                lineQueue.Add(innerLine);
                            }

                            // push the line into the buffer (verbatim)
                            defineBuffer.Add(innerLine);

                            if (innerLine.StartsWith(((byte)OpCode.END).ToString() + " "))
                                defineNesting--;

                            // if nested DEFINEs are possible, increment on new DEFINE lines
                            if (innerLine.StartsWith(((byte)OpCode.DEFINE).ToString() + " "))
                                defineNesting++;
                        }

                        // create a task that will rehydrate and compile the buffered block
                        var localBuffer = defineBuffer.ToArray();
                        var localType = defineType;
                        var localCompiler = defineCompiler;
                        var localRef = defineRefId;

                        Task<byte[]> compileTask = Task.Run(() =>
                        {
                            object instance = Rehydrate(localBuffer.ToList(), localType!);

                            using var ms = memoryStreamPool.Using();
                            using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

                            bw.Write((byte)0x00);
                            bw.Write((byte)0x00);
                            bw.Write(localCompiler!.CompilerId);
                            bw.Write(localRef);

                            localCompiler.Compile(bw, instance);
                            bw.Flush();
                            return ms.Item.ToArray();
                        }, ct);

                        byteTaskQueue.Add(compileTask);
                        continue;
                    }
                }

                using var ms = memoryStreamPool.Using();
                using var writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);
                EmitOpcode(writer, line, parts, opcodeByte, opcode);
                writer.Flush();

                byteTaskQueue.Add(Task.FromResult(ms.Item.ToArray()));
            }

            // finished producing byte tasks
            byteTaskQueue.CompleteAdding();

            // wait for writer to finish writing all byte tasks
            await writeTask.ConfigureAwait(false);
        }
        finally
        {
            // ensure writer finishes if cancellation happened
            try { byte[] endMarker = new byte[] { (byte)OpCode.END_OF_DATA }; }
            catch { }
        }
    }


    private void EmitOpcode(BinaryWriter writer, string line, string[] parts, byte opcodeByte, OpCode opcode)
    {
        if (opcode is OpCode.DEFINE)
        {
            Type t = WinterForgeVM.ResolveType(parts[1]);

            if (allowCustomCompilers)
                if (CustomValueCompilerRegistry.TryGetByType(t, out ICustomValueCompiler customCompiler))
                {
                    bufferedObject = new();
                    bufferedType = t;
                    bufferedObject.Add(line);
                    bufferedObjectRefID = int.Parse(parts[2]); // object id;
                    nesting = 1;
                    this.customCompiler = customCompiler;

                    return;
                }

            instanceStack.Push(t);
            writer.Write(opcodeByte);
            WriteString(writer, parts[1]); // type name
            WriteInt(writer, int.Parse(parts[2])); // object id
            WriteInt(writer, int.Parse(parts[3])); // arg count
            return;
        }

        writer.Write(opcodeByte);

        switch (opcode)
        {
            case OpCode.SET:
            case OpCode.SETACCESS:
                {
                    WriteString(writer, parts[1]);

                    if (instanceStack.TryPeek(out Type targetType) && opcode is not OpCode.SETACCESS)
                    {
                        ReflectionHelper rh = new(targetType);
                        MemberData mem = rh.GetMember(parts[1]);
                        Type fieldType = mem.Type;
                        ValuePrefix prefix = GetNumericValuePrefix(fieldType);

                        if (parts[2] is not "#stack()"
                            && CustomValueCompilerRegistry.TryGetByType(fieldType, out ICustomValueCompiler compiler)
                            && parts[2].GetType() == compiler.CompilerType)
                        {
                            writer.Write((byte)0x00);
                            writer.Write((byte)0x00);
                            writer.Write(compiler.CompilerId);
                            writer.Write(-1);
                            compiler.Compile(writer, parts[2]);
                        }
                        else if (prefix is ValuePrefix.NONE)
                            WriteAny(writer, parts[2]);
                        else
                            WritePrefered(writer, parts[2], prefix);
                    }
                    else
                        WritePrefered(writer, parts[2], ValuePrefix.STRING);
                }
                break;

            case OpCode.END:
                instanceStack.Pop();
                goto case OpCode.AS;
            case OpCode.AS:
                WritePrefered(writer, parts[1], ValuePrefix.INT);
                break;

            case OpCode.RET:
                WritePrefered(writer, parts[1], ValuePrefix.INT);
                break;


            case OpCode.PUSH:
                WritePrefered(writer, parts[1], ValuePrefix.STRING);
                break;

            case OpCode.ELEMENT:
                writer.Write((byte)(parts.Length - 1));
                WriteAny(writer, parts[1]);
                if (parts.Length > 2)
                    WriteAny(writer, parts[2]);
                break;

            case OpCode.LIST_START:
                WriteString(writer, parts[1]);
                if (parts.Length == 3)
                    WriteString(writer, parts[2]);
                break;

            case OpCode.ACCESS:
                WriteString(writer, parts[1]);
                break;

            case OpCode.START_STR:
            case OpCode.STR:
                WriteMultilineString(writer, line);
                break;

            case OpCode.ALIAS:
                WriteInt(writer, int.Parse(parts[1]));
                WriteString(writer, parts[2]);
                break;

            case OpCode.ANONYMOUS_SET:
                {
                    WriteString(writer, parts[1]); // type
                    WriteString(writer, parts[2]); // field

                    Type fieldDeclarerType = WinterForgeVM.ResolveType(parts[1]);
                    ReflectionHelper rh = new(fieldDeclarerType);
                    Type fieldType = WinterForgeVM.ResolveType(parts[1]);
                    ValuePrefix prefix = GetNumericValuePrefix(fieldType);
                    if (prefix is ValuePrefix.NONE)
                        WriteAny(writer, parts[3]);
                    else
                        WritePrefered(writer, parts[3], prefix);
                }
                break;

            case OpCode.IMPORT:
                WriteString(writer, parts[1]);
                WriteInt(writer, int.Parse(parts[2]));
                break;


            case OpCode.LIST_END:
            case OpCode.PROGRESS:
            case OpCode.END_STR:
            case OpCode.ADD:
            case OpCode.SUB:
            case OpCode.MUL:
            case OpCode.DIV:
            case OpCode.MOD:
            case OpCode.POW:
            case OpCode.NEG:
            case OpCode.EQ:
            case OpCode.NEQ:
            case OpCode.GT:
            case OpCode.LT:
            case OpCode.GTE:
            case OpCode.LTE:
            case OpCode.AND:
            case OpCode.NOT:
            case OpCode.OR:
            case OpCode.XOR:
            case OpCode.DEFINE:
            case OpCode.SCOPE_PUSH:
            case OpCode.SCOPE_POP:
            case OpCode.VOID_STACK_ITEM:
                // no args
                break;

            case OpCode.CALL:
                WriteString(writer, parts[1]);
                WritePrefered(writer, parts[2], ValuePrefix.INT);
                break;

            case OpCode.CONSTRUCTOR_START:
            case OpCode.TEMPLATE_CREATE: // 37
                {
                    // parts: [ "37", "<templateName>", "<paramCount>", "<type1>", "<name1>", ... ]
                    string templateName = parts[1];
                    WriteString(writer, templateName);

                    int paramCount = 0;
                    if (parts.Length >= 3)
                        paramCount = int.Parse(parts[2]);
                    WriteInt(writer, paramCount);

                    // write pairs of (type, name)
                    int idx = 3;
                    for (int p = 0; p < paramCount && idx + 1 < parts.Length; p++, idx += 2)
                    {
                        WriteString(writer, parts[idx]);     // param type
                        WriteString(writer, parts[idx + 1]); // param name
                    }
                }
                break;

            case OpCode.TEMPLATE_END: // 38
                {
                    // parts: [ "38", "<templateName>" ]
                    if (parts.Length > 1)
                        WriteString(writer, parts[1]);
                }
                break;

            case OpCode.CONTAINER_START: // 39
                {
                    // parts: [ "39", "<containerName>" ]
                    if (parts.Length > 1)
                        WriteString(writer, parts[1]);
                }
                break;

            case OpCode.CONTAINER_END: // 40
                {
                    // parts: [ "40", "<containerName>" ]
                    if (parts.Length > 1)
                        WriteString(writer, parts[1]);
                }
                break;


            case OpCode.CONSTRUCTOR_END: // 42
                {
                    // parts: [ "42", "<containerName>" ]
                    if (parts.Length > 1)
                        WriteString(writer, parts[1]);
                }
                break;

            case OpCode.VAR_DEF_START: // 43
                {
                    // parts: [ "43", "<varName>" ]
                    if (parts.Length > 1)
                        WriteString(writer, parts[1]);
                    // TODO: make default value work again now that we compile in parallel to parsing
                }
                break;

            case OpCode.VAR_DEF_END: // 44
                {
                    // parts: [ "44", "<varName>" ]
                    if (parts.Length > 1)
                        WriteString(writer, parts[1]);
                }
                break;

            case OpCode.FORCE_DEF_VAR: // 45
                {
                    // parts: [ "45", "<varName>" ]
                    if (parts.Length > 1)
                        WriteString(writer, parts[1]);
                }
                break;

            case OpCode.JUMP:
            case OpCode.JUMP_IF_FALSE:
            case OpCode.LABEL:
                WriteString(writer, parts[1]);
                break;
            default:
                throw new InvalidOperationException($"Opcode not implemented: {opcode}");
        }
    }

    private string[] SplitOpcodeLine(string line)
    {
        var parts = new List<string>();
        int i = 0;
        while (i < line.Length)
        {
            if (char.IsWhiteSpace(line[i])) { i++; continue; }

            if (line[i] == '"')
            {
                int end = line.IndexOf('"', i + 1);
                try
                {
                    parts.Add(line.Substring(i, end - i + 1));
                }
                catch (ArgumentOutOfRangeException)
                {
                    throw new WinterForgeFormatException(line);
                }
                i = end + 1;
            }
            else
            {
                int start = i;
                while (i < line.Length && !char.IsWhiteSpace(line[i])) i++;
                parts.Add(line[start..i]);
            }
        }
        return parts.ToArray();
    }

    private void WriteString(BinaryWriter writer, string str)
    {
        writer.Write((byte)ValuePrefix.STRING);
        writer.Write(str.Length);
        writer.Write(Encoding.UTF8.GetBytes(str));
    }

    private void WriteInt(BinaryWriter writer, int value)
    {
        writer.Write((byte)ValuePrefix.INT);
        writer.Write(value);
    }

    private void WritePrefered(BinaryWriter writer, object value, ValuePrefix prefered)
    {
        switch (prefered)
        {
            case ValuePrefix.BOOL when TryConvert(value, out bool boolVal):
                writer.Write((byte)ValuePrefix.BOOL);
                writer.Write(boolVal);
                break;
            case ValuePrefix.BYTE when TryConvert(value, out byte byteVal):
                writer.Write((byte)ValuePrefix.BYTE);
                writer.Write(byteVal);
                break;
            case ValuePrefix.SBYTE when TryConvert(value, out sbyte sbyteVal):
                writer.Write((byte)ValuePrefix.SBYTE);
                writer.Write(sbyteVal);
                break;
            case ValuePrefix.SHORT when TryConvert(value, out short shortVal):
                writer.Write((byte)ValuePrefix.SHORT);
                writer.Write(shortVal);
                break;
            case ValuePrefix.USHORT when TryConvert(value, out ushort ushortVal):
                writer.Write((byte)ValuePrefix.USHORT);
                writer.Write(ushortVal);
                break;
            case ValuePrefix.INT when TryConvert(value, out int intVal):
                writer.Write((byte)ValuePrefix.INT);
                writer.Write(intVal);
                break;
            case ValuePrefix.UINT when TryConvert(value, out uint uintVal):
                writer.Write((byte)ValuePrefix.UINT);
                writer.Write(uintVal);
                break;
            case ValuePrefix.LONG when TryConvert(value, out long longVal):
                writer.Write((byte)ValuePrefix.LONG);
                writer.Write(longVal);
                break;
            case ValuePrefix.ULONG when TryConvert(value, out ulong ulongVal):
                writer.Write((byte)ValuePrefix.ULONG);
                writer.Write(ulongVal);
                break;
            case ValuePrefix.FLOAT when TryConvert(value, out float floatVal):
                writer.Write((byte)ValuePrefix.FLOAT);
                writer.Write(floatVal);
                break;
            case ValuePrefix.DOUBLE when TryConvert(value, out double doubleVal):
                writer.Write((byte)ValuePrefix.DOUBLE);
                writer.Write(doubleVal);
                break;
            case ValuePrefix.DECIMAL when TryConvert(value, out decimal decimalVal):
                writer.Write((byte)ValuePrefix.DECIMAL);
                writer.Write(decimalVal);
                break;
            case ValuePrefix.CHAR when TryConvert(value, out char charVal):
                writer.Write((byte)ValuePrefix.CHAR);
                writer.Write(charVal);
                break;

            case ValuePrefix.STRING:
            default:
                if (value is string raw)
                {
                    if (raw.StartsWith("#ref(") && raw.EndsWith(")"))
                    {
                        writer.Write((byte)ValuePrefix.REF);
                        writer.Write(int.Parse(raw[5..^1]));
                    }
                    else if (raw.StartsWith("#stack(") && raw.EndsWith(")"))
                    {
                        writer.Write((byte)ValuePrefix.STACK);
                    }
                    else if (raw == "default")
                    {
                        writer.Write((byte)ValuePrefix.DEFAULT);
                    }
                    //else if (raw[0] == '"' && raw[^1] == '"')
                    //{
                    //    WriteString(writer, raw[1..^1]);
                    //}
                    else
                        WriteString(writer, raw);
                }
                else
                    WriteString(writer, value.ToString());
                break;
        }
    }

    private bool TryConvert<T>(object input, out T result)
    {
        try
        {
            if (input is T t)
            {
                result = t;
                return true;
            }

            if (input is string s)
            {
                result = (T)Convert.ChangeType(s, typeof(T));
                return true;
            }

            result = (T)Convert.ChangeType(input, typeof(T));
            return true;
        }
        catch
        {
            result = default!;
            return false;
        }
    }

    private void WriteAny(BinaryWriter writer, string raw)
    {
        if (raw.StartsWith("\""))
            WriteString(writer, raw.Trim('"'));
        else if (raw.StartsWith("#ref(") && raw.EndsWith(")"))
        {
            writer.Write((byte)ValuePrefix.REF);
            writer.Write(int.Parse(raw[5..^1]));
        }
        else if (raw.StartsWith("#stack(") && raw.EndsWith(")"))
        {
            writer.Write((byte)ValuePrefix.STACK);
        }
        else if (raw == "default")
        {
            writer.Write((byte)ValuePrefix.DEFAULT);
        }
        else if (raw.Count(c => c == '.') > 1)
        {
            WriteString(writer, raw);
        }
        else if (bool.TryParse(raw, out bool boolVal))
        {
            writer.Write((byte)ValuePrefix.BOOL);
            writer.Write(boolVal);
        }
        else if (byte.TryParse(raw, out byte byteVal))
        {
            writer.Write((byte)ValuePrefix.BYTE);
            writer.Write(byteVal);
        }
        else if (sbyte.TryParse(raw, out sbyte sbyteVal))
        {
            writer.Write((byte)ValuePrefix.SBYTE);
            writer.Write(sbyteVal);
        }
        else if (short.TryParse(raw, out short shortVal))
        {
            writer.Write((byte)ValuePrefix.SHORT);
            writer.Write(shortVal);
        }
        else if (ushort.TryParse(raw, out ushort ushortVal))
        {
            writer.Write((byte)ValuePrefix.USHORT);
            writer.Write(ushortVal);
        }
        else if (int.TryParse(raw, out int intVal))
        {
            writer.Write((byte)ValuePrefix.INT);
            writer.Write(intVal);
        }
        else if (uint.TryParse(raw, out uint uintVal))
        {
            writer.Write((byte)ValuePrefix.UINT);
            writer.Write(uintVal);
        }
        else if (long.TryParse(raw, out long longVal))
        {
            writer.Write((byte)ValuePrefix.LONG);
            writer.Write(longVal);
        }
        else if (ulong.TryParse(raw, out ulong ulongVal))
        {
            writer.Write((byte)ValuePrefix.ULONG);
            writer.Write(ulongVal);
        }
        else if (float.TryParse(raw.Replace('.', ','), out float floatVal))
        {
            writer.Write((byte)ValuePrefix.FLOAT);
            writer.Write(floatVal);
        }
        else if (double.TryParse(raw.Replace('.', ','), out double doubleVal))
        {
            writer.Write((byte)ValuePrefix.DOUBLE);
            writer.Write(doubleVal);
        }
        else if (decimal.TryParse(raw.Replace('.', ','), out decimal decimalVal))
        {
            writer.Write((byte)ValuePrefix.DECIMAL);
            writer.Write(decimalVal);
        }
        else if (char.TryParse(raw, out char charVal))
        {
            writer.Write((byte)ValuePrefix.CHAR);
            writer.Write(charVal);
        }
        else if (TryExtractWrappedType(raw, out Type t, out string v))
        {
            WritePrefered(writer, v, GetNumericValuePrefix(t));
        }
        else
        {
            WriteString(writer, raw);
        }
    }

    private static readonly Regex WRAPPED_TYPE_REGEX = new(@"\|(byte|sbyte|short|ushort|int|uint|long|ulong|float|double|decimal|char)\|", RegexOptions.Compiled);

    public static bool TryExtractWrappedType(string input, out Type type, out string raw)
    {
        Match match = WRAPPED_TYPE_REGEX.Match(input);
        if (match.Success)
        {
            string typeName = match.Groups[1].Value;
            raw = input[(typeName.Length + 2)..];

            type = typeName switch
            {
                "byte" => typeof(byte),
                "sbyte" => typeof(sbyte),
                "short" => typeof(short),
                "ushort" => typeof(ushort),
                "int" => typeof(int),
                "uint" => typeof(uint),
                "long" => typeof(long),
                "ulong" => typeof(ulong),
                "float" => typeof(float),
                "double" => typeof(double),
                "decimal" => typeof(decimal),
                "char" => typeof(char),
                _ => null
            };

            return type != null;
        }

        type = null;
        raw = input;
        return false;
    }

    private void WriteMultilineString(BinaryWriter writer, string fullLine)
    {
        writer.Write((byte)ValuePrefix.MULTILINE_STRING);
        var content = fullLine[2..].Trim();
        WriteString(writer, content);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // if any leftover text exists that didn't end with newline, treat it as a final line
            if (writeBuffer.Length > 0)
            {
                string leftover = writeBuffer.ToString();
                writeBuffer.Clear();
                lineQueue.Add(leftover);
            }

            // mark input complete
            lineQueue.CompleteAdding();

            // wait for processing to finish
            try
            {
                backgroundProcessingTask?.GetAwaiter().GetResult();
            }
            catch
            {
                // rethrow after disposing resources
                cancellation.Cancel();
                throw;
            }
            finally
            {
                cancellation.Cancel();
                cancellation.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}


