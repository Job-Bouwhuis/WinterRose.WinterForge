
using System.Text;
using WinterRose.WinterForgeSerializing.Instructions;

namespace WinterRose.WinterForgeSerializing;

internal class OpcodeToReadableOpcodeParser
{
    internal void Parse(Stream mem, Stream opcodes)
    {
        using StreamReader reader = new StreamReader(mem, leaveOpen: true);
        using StreamWriter writer = new StreamWriter(opcodes, leaveOpen: true);

        string? rawLine;
        while ((rawLine = reader.ReadLine()) != null)
        {
            var line = rawLine.Trim();

            if (line == "WF_ENDOFDATA")
            {
                writer.WriteLine(line);
                break; // network stream end of data mark
            }

            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//"))
            {
                writer.WriteLine(line);
                continue;
            }

            var parts = TokenizeLine(line);
            if (parts.Count == 0)
            {
                writer.WriteLine(line);
                continue;
            }

            OpCode opcode = (OpCode)int.Parse(parts[0]);
            string newline = $"{opcode.ToString()} {string.Join(' ', parts.Skip(1))}".Trim();
            writer.WriteLine(newline);
        }
        writer.Flush();
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