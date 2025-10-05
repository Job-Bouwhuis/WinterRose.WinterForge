using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WinterRose.WinterForgeSerializing.Instructions;

namespace WinterRose.WinterForgeSerializing
{
    public static class InstructionParser
    {
        /// <summary>
        /// Parses opcodes into instructions. inserting a progress mark every <paramref name="progressInterval"/>. <br></br>
        /// eg: if <paramref name="progressInterval"/> is 20, 3 will be inserted
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="progressInterval"></param>
        /// <returns></returns>
        public static List<Instruction> ParseOpcodes(Stream stream, int progressInterval = -1)
        {
            var instructions = new List<Instruction>();

            StreamReader reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);

            string? rawLine;
            while ((rawLine = reader.ReadLine()) != null)
            {
                var line = rawLine.Trim();

                if (line == "WF_ENDOFDATA")
                    break; // network stream end of data mark

                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//"))
                    continue;

                var commentIndex = line.IndexOf("//");
                if (commentIndex >= 0)
                    line = line[..commentIndex].Trim();

                var parts = TokenizeLine(line);
                if (parts.Count == 0)
                    continue;

                OpCode opcode = (OpCode)int.Parse(parts[0]);
                var args = new List<object>();

                switch (opcode)
                {
                    case OpCode.DEFINE:
                        args.Add(parts[1]);                     // type name (string)
                        args.Add(int.Parse(parts[2]));          // object ID
                        args.Add(int.Parse(parts[3]));          // arg count
                        break;

                    case OpCode.SET:
                    case OpCode.SETACCESS:
                        args.Add(parts[1]);                     // field name
                        args.Add(parts[2]);                     // value (left as string — for ReadAny)
                        break;

                    case OpCode.END:
                    case OpCode.RET:
                    case OpCode.AS:
                        args.Add(int.Parse(parts[1]));
                        break;

                    case OpCode.PUSH:
                        args.Add(parts[1]);                     // value (ReadAny)
                        break;

                    case OpCode.ELEMENT:
                        args.Add(byte.Parse(parts[1]));         // count
                        args.Add(parts[2]);                     // 1st value (ReadAny)
                        if (parts[1] == "2")
                            args.Add(parts[3]);                 // 2nd value (ReadAny)
                        break;

                    case OpCode.LIST_START:
                        args.Add(parts[1]);                     // list type
                        if (parts.Count > 2)
                            args.Add(parts[2]);                 // optional type arg
                        break;

                    case OpCode.ACCESS:
                        args.Add(parts[1]);                     // field name
                        break;

                    case OpCode.START_STR:
                    case OpCode.STR:
                        args.Add(parts[1]);                     // string literal (assumed single-line here)
                        break;

                    case OpCode.ANONYMOUS_SET:
                        args.Add(parts[1]);                     // type
                        args.Add(parts[2]);                     // name
                        args.Add(parts[3]);                     // value (ReadAny)
                        break;

                    case OpCode.IMPORT:
                        args.Add(parts[1]);                     // filename
                        args.Add(int.Parse(parts[2]));          // ref id
                        break;

                    // No-arg opcodes:
                    case OpCode.LIST_END:
                    case OpCode.PROGRESS:
                    case OpCode.END_STR:
                        break;

                    default:
                        throw new InvalidOperationException($"Opcode {opcode} not supported in line parser.");
                }

                instructions.Add(new Instruction(opcode, args.ToArray()));
            }



            int count = instructions.Count;
            Instruction progressInstr = new(OpCode.PROGRESS, []);

            if (progressInterval > 0)
            {
                if (progressInterval > 100)
                    progressInterval = 100;

                int insertCount = 100 / progressInterval;
                int step = count / insertCount;

                instructions.Insert(0, progressInstr);
                instructions.Insert(instructions.Count - 1, progressInstr);

                if (step is 0)
                    return instructions;

                for (int i = step; i < count; i += step)
                {
                    instructions.Insert(i, progressInstr);
                    count++; // Because we just added an item, so the count increases
                    i++;     // Adjust index to avoid infinite loop due to shift
                }
            }

            return instructions;
        }

        private static List<string> TokenizeLine(string line)
        {
            var tokens = new List<string>();
            var sb = new StringBuilder();
            bool insideQuotes = false;

            foreach (char c in line)
            {
                if (c == '"')
                {
                    insideQuotes = !insideQuotes;
                    if (!insideQuotes && sb.Length == 0)
                    {
                        tokens.Add("");
                        continue;
                    }
                }
                else if (char.IsWhiteSpace(c) && !insideQuotes)
                {
                    if (sb.Length > 0)
                    {
                        tokens.Add(sb.ToString());
                        sb.Clear();
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }

            if (sb.Length > 0)
                tokens.Add(sb.ToString());

            return tokens;
        }
    }

}
