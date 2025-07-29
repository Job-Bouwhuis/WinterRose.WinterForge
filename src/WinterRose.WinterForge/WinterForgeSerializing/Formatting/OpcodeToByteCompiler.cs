using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinterRose.Reflection;
using WinterRose.WinterForgeSerializing;
using WinterRose.WinterForgeSerializing.Workers;

namespace WinterRose.WinterForgeSerializing.Formatting;

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



public class OpcodeToByteCompiler
{
    private Stack<Type> instanceStack = new Stack<Type>();

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


    public void Compile(Stream textOpcodes, Stream bytesDestination)
    {
        using var reader = new StreamReader(textOpcodes, leaveOpen: true);
        using var writer = new BinaryWriter(bytesDestination, System.Text.Encoding.UTF8, leaveOpen: true);

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line == "WF_ENDOFDATA")
                writer.Write((byte)OpCode.END_OF_DATA);
            if (line.StartsWith("//")) // skip comments
            {
                continue;
            }
            var parts = SplitOpcodeLine(line.Trim());
            if (parts.Length == 0) continue;

            if (!byte.TryParse(parts[0], out byte opcodeInt) || !Enum.IsDefined(typeof(OpCode), opcodeInt))
                throw new InvalidOperationException($"Invalid opcode: {parts[0]}");

            var opcode = (OpCode)opcodeInt;
            writer.Write((byte)opcodeInt);

            switch (opcode)
            {
                case OpCode.DEFINE:
                    Type t = InstructionExecutor.ResolveType(parts[1]);
                    instanceStack.Push(t);

                    WriteString(writer, parts[1]); // type name
                    WriteInt(writer, int.Parse(parts[2])); // object id
                    WriteInt(writer, int.Parse(parts[3])); // arg count
                    break;

                case OpCode.SET:
                case OpCode.SETACCESS:
                    {
                        WriteString(writer, parts[1]);

                        if (instanceStack.TryPeek(out Type targetType))
                        {
                            ReflectionHelper rh = new(targetType);
                            MemberData mem = rh.GetMember(parts[1]);
                            Type fieldType = mem.Type;
                            ValuePrefix prefix = GetNumericValuePrefix(fieldType);
                            if (prefix is ValuePrefix.NONE)
                                WriteAny(writer, parts[2]);
                            else
                                WritePrefered(writer, parts[2], prefix);
                        }
                        else
                            WriteAny(writer, parts[2]);
                    }
                    break;

                case OpCode.END:
                    instanceStack.Pop();
                    goto case OpCode.RET;
                case OpCode.RET:
                case OpCode.AS:
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

                case OpCode.LIST_END:
                case OpCode.PROGRESS:
                case OpCode.END_STR:
                    break; // no args

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

                        Type fieldDeclarerType = InstructionExecutor.ResolveType(parts[1]);
                        ReflectionHelper rh = new(fieldDeclarerType);
                        MemberData mem = rh.GetMember(parts[1]);
                        Type fieldType = mem.Type;
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

                default:
                    throw new InvalidOperationException($"Opcode not implemented: {opcode}");
            }
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
                parts.Add(line.Substring(i + 1, end - i - 1));
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
        writer.Write(System.Text.Encoding.UTF8.GetBytes(str));
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
            case ValuePrefix.STRING:
                WriteString(writer, value.ToString());
                break;
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
            default:
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
                result = (T)System.Convert.ChangeType(s, typeof(T));
                return true;
            }

            result = (T)System.Convert.ChangeType(input, typeof(T));
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
        {
            WriteString(writer, raw.Trim('"'));
        }
        else if (raw.StartsWith("_ref(") && raw.EndsWith(")"))
        {
            writer.Write((byte)ValuePrefix.REF);
            writer.Write(int.Parse(raw[5..^1]));
        }
        else if (raw.StartsWith("_stack(") && raw.EndsWith(")"))
        {
            writer.Write((byte)ValuePrefix.STACK);
        }
        else if (raw == "default")
        {
            writer.Write((byte)ValuePrefix.DEFAULT);
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
        else
        {
            WriteString(writer, raw);
        }
    }

    private void WriteMultilineString(BinaryWriter writer, string fullLine)
    {
        writer.Write((byte)ValuePrefix.MULTILINE_STRING);
        var content = fullLine[2..].Trim();
        WriteString(writer, content);
    }
}


