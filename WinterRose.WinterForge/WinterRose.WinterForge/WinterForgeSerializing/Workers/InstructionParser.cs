using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WinterRose.WinterForgeSerializing.Workers;

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

                // Skip comments and empty lines
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//"))
                    continue;

                // Remove inline comments
                var commentIndex = line.IndexOf("//");
                if (commentIndex >= 0)
                    line = line[..commentIndex].Trim();

                // Tokenize line: opcode and arguments
                var parts = TokenizeLine(line);
                if (parts.Length == 0)
                    continue;

                // Parse OpCode
                // enum tryparse is slow. opt for numerical opcodes in the future
                // when a parser exists to convert the human readable format into computer format

                OpCode opcode = (OpCode)int.Parse(parts[0]);

                //if (!Enum.TryParse(parts[0], ignoreCase: true, out OpCode opcode))
                //throw new Exception($"Invalid opcode: {parts[0]}");

                // Add instruction
                instructions.Add(new Instruction(opcode, parts.Skip(1).ToArray()));
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

        private static string[] TokenizeLine(string line)
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

            return tokens.ToArray();
        }
    }

}
