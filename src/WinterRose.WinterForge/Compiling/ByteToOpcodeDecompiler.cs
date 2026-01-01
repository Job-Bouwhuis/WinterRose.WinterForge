using System.IO;
using System.Net;
using System.Threading.Channels;
using WinterRose.NetworkServer;
using WinterRose.WinterForgeSerializing.Instructions;

namespace WinterRose.WinterForgeSerializing.Compiling;

public class ByteToOpcodeDecompiler
{
    public static bool WaitIndefinitelyForData { get; set; } = false;

    public static InstructionStream Parse(Stream byteStream, bool threaded = true)
    {
        var instructionStream = new InstructionStream();

        if (threaded)
            ThreadPool.QueueUserWorkItem(_ => DoParsing(byteStream, instructionStream));
        else
            DoParsing(byteStream, instructionStream); 
        return instructionStream;
    }

    private static void DoParsing(Stream byteStream, InstructionStream instructionStream)
    {
        using CacheReader cacheStream = new(byteStream, new MemoryStream());
        try
        {
            using var reader = new BinaryReader(cacheStream, System.Text.Encoding.UTF8, leaveOpen: true);
            InternalParse(reader, instructionStream);
            instructionStream.Complete();
        }
        catch (InvalidOperationException e)
        {
            using DualStreamReader cache = cacheStream.CreateFallbackReader();
            try
            {
                using MemoryStream opcodes = new MemoryStream();
                WinterForge.ConvertFromStreamToStream(cache, opcodes, TargetFormat.Optimized);
                opcodes.Position = 0;
                using var reader = new BinaryReader(opcodes, System.Text.Encoding.UTF8, leaveOpen: false);
                InternalParse(reader, instructionStream);
                instructionStream.Complete();
            }
            catch
            {
                instructionStream.Fail(e);
            }
        }
        catch (Exception e)
        {
            instructionStream.Fail(e);
        }
    }

    private static void InternalParse(BinaryReader reader, InstructionStream instructions)
    {
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
                    case OpCode.SCOPE_PUSH:
                    case OpCode.SCOPE_POP:
                    case OpCode.VOID_STACK_ITEM:
                        // no args
                        break;

                    case OpCode.JUMP:
                    case OpCode.JUMP_IF_FALSE:
                    case OpCode.LABEL:
                        args.Add(ReadString(reader));
                        break;

                    case OpCode.CALL:
                        // prints: CALL <methodName> <argument count>
                        args.Add(ReadString(reader));
                        args.Add(ReadInt(reader));
                        break;

                    case OpCode.CONSTRUCTOR_START:
                    case OpCode.TEMPLATE_CREATE: // 37
                        // prints: TEMPLATE_CREATE <templateName> <paramCount> <type1> <name1> <type2> <name2> ...
                        args.Add(ReadString(reader));
                        int argCount = ReadInt(reader);
                        args.Add(argCount);
                        for (int i = 0; i < argCount; i++)
                        {
                            args.Add(ReadString(reader)); //type
                            args.Add(ReadString(reader)); //name
                        }
                        break;

                    case OpCode.VAR_DEF_START: // 43
                        // prints: VAR_DEF_START <varName> [<defaultValue> if present]
                        args.Add(ReadString(reader));
                        byte next = (byte)reader.PeekChar();
                        if (next == (byte)OpCode.VAR_DEFAULT_VALUE)
                        {
                            reader.Read(); // consume default value prefix
                            args.Add(ReadAny(reader));
                        }
                        break;

                    case OpCode.TEMPLATE_END: // 38
                    case OpCode.CONTAINER_START:
                    case OpCode.CONTAINER_END: // 40
                    case OpCode.CONSTRUCTOR_END: // 42
                    case OpCode.VAR_DEF_END: // 44
                    case OpCode.FORCE_DEF_VAR: // 45
                        args.Add(ReadString(reader));
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

        if(type is ValuePrefix.NONE)
        {
            // see if theres a custom value compiler, if so, use it
            int isCustomCompiler = reader.PeekChar();
            if (isCustomCompiler != 0)
                throw new InvalidDataException($"Unknown type prefix {(byte)type}");

            reader.ReadByte(); // consume marker

            uint compilerID = reader.ReadUInt32();
            int objectId = reader.ReadInt32();
            if (CustomValueCompilerRegistry.TryGetById(compilerID, out var compiler))
            {
                return compiler.Decompile(reader);
            }
            else
                throw new InvalidOperationException($"Expected compiler with id {compilerID} to exist, but it didn't");
        }

        return type switch
        {
            ValuePrefix.STRING => ReadString(reader, true),
            ValuePrefix.INT => reader.ReadInt32(),
            ValuePrefix.REF => $"#ref({reader.ReadInt32()})",
            ValuePrefix.STACK => "#stack()",
            ValuePrefix.DEFAULT => "default",
            ValuePrefix.BOOL => reader.ReadBoolean(),
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
            _ => throw new InvalidDataException($"Unknown type prefix {type}")
        };
    }
}


