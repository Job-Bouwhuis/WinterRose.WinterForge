using System.IO;
using System.Threading.Channels;
using WinterRose.NetworkServer;
using WinterRose.WinterForgeSerializing.Instructions;

namespace WinterRose.WinterForgeSerializing.Compiling;

public class ByteToOpcodeParser
{
    public static bool WaitIndefinitelyForData { get; set; } = false;

    public static List<Instruction> Parse(Stream byteStream)
    {
        using CacheReader cacheStream = new(byteStream, new MemoryStream());
        try
        {
            using var reader = new BinaryReader(cacheStream, System.Text.Encoding.UTF8, leaveOpen: true);
            return InternalParse(reader);
        }
        catch (InvalidOperationException e)
        {
            using DualStreamReader cache = cacheStream.CreateFallbackReader();
            return InstructionParser.ParseOpcodes(cache);
        }
    }

    private static List<Instruction> InternalParse(BinaryReader reader)
    {
        List<Instruction> instructions = [];

        try
        {
            while (true)
            {
                byte peek = reader.ReadByte();
                if (peek == -1)
                {
                    if (WaitIndefinitelyForData)
                    {
                        Task.Yield();
                        continue;
                    }
                    else
                        break;
                }

                OpCode opcode = (OpCode)peek;
                var args = new List<object>();

                switch (opcode)
                {
                    case OpCode.END_OF_DATA:
                        break;

                    case OpCode.DEFINE:
                        if (reader.PeekChar() == '\0')
                        {
                            reader.ReadByte(); // consume marker
                            uint compilerID = reader.ReadUInt32();
                            if (CustomValueCompilerRegistry.TryGetById(compilerID, out var compiler))
                            {
                                int refID = reader.ReadInt32();
                                object o = compiler.Decompile(reader);
                                instructions.Add(new Instruction(OpCode.CREATE_REF, [refID, o]));
                                continue;
                            }
                            else
                                throw new InvalidOperationException($"Expected compiler with id {compilerID} to exist, but it didn't");
                        }

                        args.Add(ReadString(reader)); // type name
                        args.Add(ReadInt(reader));    // object ID
                        args.Add(ReadInt(reader));    // arg count
                        break;

                    case OpCode.SET:
                    case OpCode.SETACCESS:
                        args.Add(ReadString(reader));
                        args.Add(ReadAny(reader));
                        break;

                    case OpCode.END:
                    case OpCode.RET:
                    case OpCode.AS:
                    case OpCode.PUSH:
                        args.Add(ReadAny(reader));
                        break;

                    case OpCode.ELEMENT:
                        int elements = reader.ReadByte();
                        args.Add(ReadAny(reader));
                        if (elements == 2)
                            args.Add(ReadAny(reader));
                        break;

                    case OpCode.LIST_START:
                        args.Add(ReadString(reader));
                        if (reader.PeekChar() == 0x01)
                            args.Add(ReadString(reader));
                        break;

                    case OpCode.ACCESS:
                        args.Add(ReadString(reader));
                        break;

                    case OpCode.START_STR:
                    case OpCode.STR:
                        args.Add(ReadMultilineString(reader));
                        break;

                    case OpCode.ANONYMOUS_SET:
                        args.Add(ReadString(reader));
                        args.Add(ReadString(reader));
                        args.Add(ReadAny(reader));
                        break;

                    case OpCode.IMPORT:
                        args.Add(ReadString(reader));
                        args.Add(ReadInt(reader));
                        break;

                    // No-arg instructions
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
                        // no args
                        break;

                    default:
                        throw new InvalidOperationException($"Opcode {opcode} not supported in deserializer.");
                }

                instructions.Add(new Instruction(opcode, args.ToArray()));

                if (opcode == OpCode.END_OF_DATA)
                    break;
            }
        }
        catch (EndOfStreamException)
        {
            // assume end of instructions giving valid data because who gives a fuck
        }

        return instructions;
    }


    private static string ReadString(BinaryReader reader, bool consumedPrefix = false)
    {
        if (!consumedPrefix)
        {
            byte prefix = reader.ReadByte();
            if (prefix != 0x01)
                throw new InvalidDataException($"Expected string prefix 0x01 but got {prefix:X2}");
        }

        int length = reader.ReadInt32();
        var bytes = reader.ReadBytes(length);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    private static int ReadInt(BinaryReader reader)
    {
        byte prefix = reader.ReadByte();
        if (prefix != 0x02)
            throw new InvalidDataException($"Expected int prefix 0x02 but got {prefix:X2}");

        return reader.ReadInt32();
    }

    private static object? ReadAny(BinaryReader reader)
    {
        ValuePrefix type = (ValuePrefix)reader.ReadByte();
        return type switch
        {
            ValuePrefix.STRING => ReadString(reader, true),
            ValuePrefix.INT => reader.ReadInt32(),
            ValuePrefix.REF => $"_ref({reader.ReadInt32()})",
            ValuePrefix.STACK => "_stack()",
            ValuePrefix.DEFAULT => "default",
            ValuePrefix.BOOL => reader.ReadBoolean(),
            ValuePrefix.MULTILINE_STRING => ReadMultilineString(reader),
            ValuePrefix.FLOAT => reader.ReadSingle(),
            ValuePrefix.SHORT => reader.ReadInt16(),
            ValuePrefix.USHORT => reader.ReadUInt16(),
            ValuePrefix.UINT => reader.ReadUInt32(),
            ValuePrefix.LONG => reader.ReadInt64(),
            ValuePrefix.ULONG => reader.ReadUInt64(),
            ValuePrefix.DOUBLE => reader.ReadDouble(),
            ValuePrefix.BYTE => reader.ReadByte(),
            ValuePrefix.SBYTE => reader.ReadSByte(),
            ValuePrefix.CHAR => reader.ReadChar(),
            ValuePrefix.DECIMAL => reader.ReadDecimal(),
            ValuePrefix.NULL => null,
            _ => throw new InvalidDataException($"Unknown type prefix {type:X2}")
        };
    }



    private static string ReadMultilineString(BinaryReader reader)
    {
        // For example, prefix 0x08 then length-prefixed string
        byte prefix = reader.ReadByte();
        if (prefix != 0x01)
            throw new InvalidDataException($"Expected string prefix 0x01 but got {prefix:X2}");

        ushort length = reader.ReadUInt16();
        var bytes = reader.ReadBytes(length);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }
}


