using System;
using System.Buffers;
using System.Collections.Concurrent;
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

    public override void Flush()
    {
        lineQueue.CompleteAdding();

        while (lineQueue.Count > 0 || (backgroundProcessingTask != null && !backgroundProcessingTask.IsCompleted))
        {
            Thread.Sleep(10);
        }

        try
        {
            backgroundProcessingTask?.GetAwaiter().GetResult();
        }
        catch
        {
            throw;
        }
    }


    internal OpcodeToByteCompiler(Stream bytesDestination, bool allowCustomCompilers = true)
    {
        this.allowCustomCompilers = allowCustomCompilers;
        this.bytesDestinationStream = bytesDestination ?? throw new ArgumentNullException(nameof(bytesDestination));

        // run a single background worker that consumes lineQueue and writes compiled bytes to destination
        backgroundProcessingTask = Task.Run(() => BackgroundProcessLinesWorker(cancellation.Token));
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

    private void BackgroundProcessLinesWorker(CancellationToken ct)
    {
        if (bytesDestinationStream == null) throw new InvalidOperationException("No bytes destination attached for streaming compile.");

        using var finalWriter = new BinaryWriter(bytesDestinationStream, Encoding.UTF8, leaveOpen: true);

        try
        {
            foreach (var rawLine in lineQueue.GetConsumingEnumerable(ct))
            {
                if (ct.IsCancellationRequested) break;

                if (rawLine == "WF_ENDOFDATA")
                {
                    SeenEndOfDataMark = true;
                    finalWriter.Write((byte)OpCode.END_OF_DATA);
                    continue;
                }

                if (!ValidateLine(rawLine, out var parts, out var opcodeByte))
                {
                    continue;
                }

                var opcode = (OpCode)opcodeByte;

                // fast path for DEFINE + custom compiler: buffer block then rehydrate + compile inline (we're already on a background thread)
                if (opcode == OpCode.DEFINE && allowCustomCompilers)
                {
                    Type t = WinterForgeVM.ResolveType(parts[1]);
                    if (CustomValueCompilerRegistry.TryGetByType(t, out ICustomValueCompiler foundCompiler))
                    {
                        var buffer = new List<string> { rawLine };
                        int nesting = 1;

                        while (nesting > 0)
                        {
                            // this will block until more lines are available or collection completed
                            string innerLine;
                            try
                            {
                                innerLine = lineQueue.Take(ct);
                            }
                            catch (OperationCanceledException) { break; }

                            if (innerLine == "WF_ENDOFDATA")
                            {
                                SeenEndOfDataMark = true;
                                // keep processing the buffer but also re-enqueue marker for any other consumers (not needed here)
                                // continue; -- we still want to add it so buffer remains consistent if it legitimately appears
                            }

                            buffer.Add(innerLine);

                            if (innerLine.StartsWith(((byte)OpCode.END).ToString() + " "))
                                nesting--;

                            if (innerLine.StartsWith(((byte)OpCode.DEFINE).ToString() + " "))
                                nesting++;
                        }

                        // rehydrate and compile into a local memory stream, then write to final stream
                        object instance = Rehydrate(buffer, t);
                        using var ms = memoryStreamPool.Using();
                        using var bw = new BinaryWriter(ms.Item, Encoding.UTF8, leaveOpen: true);

                        bw.Write((byte)0x00);
                        bw.Write((byte)0x00);
                        bw.Write(foundCompiler.CompilerId);
                        bw.Write(int.Parse(parts[2])); // ref id from the DEFINE header
                        foundCompiler.Compile(bw, instance);
                        bw.Flush();

                        var outBytes = ms.Item.ToArray();
                        if (outBytes.Length > 0)
                            finalWriter.Write(outBytes);

                        continue;
                    }
                }

                // standard opcode path: emit to a pooled memory stream then write to final stream
                using (var ms = memoryStreamPool.Using())
                using (var writer = new BinaryWriter(ms.Item, Encoding.UTF8, leaveOpen: true))
                {
                    EmitOpcode(writer, rawLine, parts, opcodeByte, opcode);
                    writer.Flush();

                    var outBytes = ms.Item.ToArray();
                    if (outBytes.Length > 0)
                        finalWriter.Write(outBytes);
                }
            }

            // if we exited normally and no explicit end-of-data marker was seen, write END_OF_DATA for network streams
            if (bytesDestinationStream is System.Net.Sockets.NetworkStream && !SeenEndOfDataMark)
            {
                finalWriter.Write((byte)OpCode.END_OF_DATA);
            }

            finalWriter.Flush();
        }
        catch (OperationCanceledException)
        {
            // cancellation requested - best effort to flush already written data
            try { finalWriter.Flush(); } catch { }
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
            // flush any leftover partial line as a final line
            if (writeBuffer.Length > 0)
            {
                string leftover = writeBuffer.ToString();
                writeBuffer.Clear();
                lineQueue.Add(leftover);
            }

            // mark input complete
            lineQueue.CompleteAdding();

            // wait for background work to finish
            try
            {
                backgroundProcessingTask?.GetAwaiter().GetResult();
            }
            catch
            {
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


