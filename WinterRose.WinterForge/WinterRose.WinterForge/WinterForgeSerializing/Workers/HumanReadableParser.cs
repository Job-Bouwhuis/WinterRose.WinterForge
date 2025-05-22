using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WinterRose.WinterForgeSerializing.Workers
{
    public class HumanReadableParser
    {
        private StreamReader reader = null!;
        private StreamWriter writer = null!;
        private string? currentLine;
        private int depth = 0;
        private Dictionary<string, int> aliasMap = [];
        private readonly Stack<OverridableStack<string>> lineBuffers = new();
        List<int> foundIds = [];
        private static readonly Dictionary<string, int> opcodeMap = Enum
            .GetValues<OpCode>()
            .ToDictionary(op => op.ToString(), op => (int)op);

        //private static readonly Dictionary<string, string> opcodeMap = Enum
        //  .GetValues<OpCode>()
        //  .ToDictionary(op => op.ToString(), op => op.ToString());

        /// <summary>
        /// Parses the human readable format of WinterForge into the opcodes that the <see cref="InstructionParser"/> understands. so that the <see cref="InstructionExecutor"/> can deserialize
        /// </summary>
        /// <param name="input">The source of human readable format</param>
        /// <param name="output">The destination where the WinterForge opcodes will end up</param>
        /// <remarks>Appends a line 'WF_ENDOFDATA' when <paramref name="output"/> is of type <see cref="NetworkStream"/></remarks>
        public void Parse(Stream input, Stream output)
        {
            reader = new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            writer = new StreamWriter(output, Encoding.UTF8, leaveOpen: true);

            while ((currentLine = ReadNonEmptyLine()) != null)
                ParseObjectOrAssignment();

            if (output is NetworkStream)
                writer.WriteLine("WF_ENDOFDATA");
            writer.Flush();
        }

        private int NextAvalible()
        {
            if (foundIds.Count == 0)
                return 0;

            foundIds.Sort();

            int lastNumber = 0;

            for (int i = 0; i < foundIds.Count; i++)
            {
                if (foundIds[i] != i)
                    return lastNumber + 1;
                lastNumber = i;
            }

            return lastNumber + 1;

        }

        private void ParseObjectOrAssignment()
        {
            string line = currentLine!.Trim();

            // Constructor Definition: Type(arguments) : ID {
            if (line.Contains('(') && line.Contains(')') && line.Contains(':') && line.Contains('{'))
            {
                int openParenIndex = line.IndexOf('(');
                int closeParenIndex = line.IndexOf(')');
                int colonIndex = line.IndexOf(':');
                int braceIndex = line.IndexOf('{');

                string type = line[..openParenIndex].Trim();
                string arguments = line.Substring(openParenIndex + 1, closeParenIndex - openParenIndex - 1).Trim();
                string id = line.Substring(colonIndex + 1, braceIndex - colonIndex - 1).Trim();

                if(id is "nextid")
                    id = NextAvalible().ToString();
                foundIds.Add(int.Parse(id));

                var args = arguments.Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                foreach (string arg in args)
                    WriteLine($"{opcodeMap["PUSH"]} " + arg);
                WriteLine($"{opcodeMap["DEFINE"]} {type} {id} {args.Length}");
                depth++;
                ParseBlock(id);
            }
            // Constructor Definition with no block: Type(arguments) : ID;
            else if (line.Contains('(') && line.Contains(')') && line.Contains(':') && line.EndsWith(";"))
            {
                int openParenIndex = line.IndexOf('(');
                int closeParenIndex = line.IndexOf(')');
                int colonIndex = line.IndexOf(':');

                string type = line[..openParenIndex].Trim();
                string arguments = line.Substring(openParenIndex + 1, closeParenIndex - openParenIndex - 1).Trim();
                string id = line.Substring(colonIndex + 1, line.Length - colonIndex - 2).Trim();

                if (id is "nextid")
                    id = NextAvalible().ToString();
                foundIds.Add(int.Parse(id));

                var args = arguments.Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                foreach (string arg in args)
                    WriteLine($"{opcodeMap["PUSH"]} " + arg);
                WriteLine($"{opcodeMap["DEFINE"]} {type} {id} {args.Length}");
                WriteLine($"{opcodeMap["END"]} {id}");
            }
            // Definition: Type : ID {
            else if (line.Contains(':') && line.Contains('{'))
            {
                int colonIndex = line.IndexOf(':');
                int braceIndex = line.IndexOf('{');

                string type = line[..colonIndex].Trim();
                string id = line.Substring(colonIndex + 1, braceIndex - colonIndex - 1).Trim();

                if (id is "nextid")
                    id = NextAvalible().ToString();
                foundIds.Add(int.Parse(id));

                WriteLine($"{opcodeMap["DEFINE"]} {type} {id} 0");
                depth++;
                ParseBlock(id);
            }
            else if (line.Contains(':') && line.EndsWith(';'))
            {
                string type;
                string id;

                var parts = line[..^1].Split(':');
                type = parts[0].Trim();
                id = parts[1].Trim();

                if (id is "nextid")
                    id = NextAvalible().ToString();
                foundIds.Add(int.Parse(id));

                WriteLine($"{opcodeMap["DEFINE"]} {type} {id} 0");
                WriteLine($"{opcodeMap["END"]} {id}");
            }
            else if (line.Contains(':'))
            {
                string type;
                string id;

                var parts = line.Split(':');
                type = parts[0].Trim();
                id = parts[1].Trim();

                if (id is "nextid")
                    id = NextAvalible().ToString();

                foundIds.Add(int.Parse(id));
                ReadNextLineExpecting("{");

                WriteLine($"{opcodeMap["DEFINE"]} {type} {id} 0");
                depth++;

                ParseBlock(id);
            }
            else if (line.StartsWith("return"))
            {
                int trimoffEnd = 0;
                if (line.EndsWith(';'))
                    trimoffEnd = 1;
                string ID = line[6..new Index(trimoffEnd, true)].Trim();
                if (string.IsNullOrWhiteSpace(ID) || !ID.All(char.IsDigit) && ID != "_stack()")
                    throw new Exception("Invalid ID parameter in RETURN statement");
                string result = $"{opcodeMap["RET"]} {ID}";
                WriteLine(result);
            }
            else if (line.Contains("->"))
            {
                HandleAccessing(null);
            }
            else if (line.StartsWith("as"))
            {
                string id = line[2..].Trim();
                if (id.EndsWith(';'))
                    id = id[..^1];

                if (id is "nextid")
                    id = NextAvalible().ToString();

                WriteLine($"{opcodeMap["AS"]} {id}");
                foundIds.Add(int.Parse(id));
            }
            else if (HasValidGenericFollowedByBracket(line))
            {
                ParseList();
            }
            else if (line.StartsWith("alias"))
            {
                string[] parts = line.Split(' ');
                int id = int.Parse(parts[1]);

                string alias = parts[2].EndsWith(';') ? parts[2][..^1] : parts[2];
                aliasMap.Add(alias, id);
            }
            else
                throw new Exception($"Unexpected top-level line: {line}");
        }

        private static bool HasValidGenericFollowedByBracket(ReadOnlySpan<char> input)
        {
            int length = input.Length;
            int i = 0;

            while (i < length && input[i] != '<') i++;
            if (i == length) return false;

            int depth = 0;
            for (; i < length; i++)
            {
                char c = input[i];
                if (c == '<')
                {
                    depth++;
                }
                else if (c == '>')
                {
                    depth--;
                    if (depth < 0) return false;
                }
                else if (c == '[' && depth == 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void ParseBlock(string id)
        {
            while ((currentLine = ReadNonEmptyLine()) != null)
            {
                string line = currentLine.Trim();

                if (line == "}")
                {
                    depth--;

                    WriteLine($"{opcodeMap["END"]} {id}");
                    return;
                }
                if (line.Contains("->"))
                    HandleAccessing(id);
                else if (line.Contains('=') && line.EndsWith(';'))
                    ParseAssignment(line);
                else if (line.Contains('=') && line.Contains('\"'))
                    ParseAssignment(line);
                else if (line.Contains(':'))
                {
                    currentLine = line;
                    ParseObjectOrAssignment(); // nested define
                }
                else if (line.StartsWith("as"))
                {
                    string asid = line[2..].Trim();
                    if (asid.EndsWith(';'))
                        asid = asid[..^1];
                    WriteLine($"{opcodeMap["AS"]} {asid}");
                }
                else if (line.StartsWith("alias"))
                {
                    string[] parts = line.Split(' ');
                    int aliasid = int.Parse(parts[0]);
                    WriteLine($"{opcodeMap["ALIAS"]} {aliasid} {parts[1]}");
                }
                else if (line.Contains('['))
                {
                    currentLine = line;

                    bool result = ParseList();

                    int equalsIndex = line.IndexOf('=');
                    if (equalsIndex != -1)
                    {
                        string name = line[..equalsIndex].Trim();
                        if (!string.IsNullOrEmpty(name))
                            WriteLine($"{opcodeMap["SET"]} {name} _stack()");
                    }

                    if (lineBuffers.Count > 0)
                    {
                        if (lineBuffers.Peek().Count >= 1)
                        {
                            string last = lineBuffers.Peek().PeekLast();
                            if (last.Trim() == "}")
                                continue;
                        }

                        return;
                    }
                }
                else
                {
                    throw new Exception($"unexpected block content: {line}");
                }
            }
        }

        private void HandleAccessing(string? id)
        {
            // Step 1: Split on '=' to separate LHS and RHS
            string[] assignmentParts = currentLine.Split('=', 2, StringSplitOptions.TrimEntries);
            string accessPart = assignmentParts[0];
            string? rhs = assignmentParts.Length > 1 ? assignmentParts[1] : null;

            // Step 2: If there's a RHS, evaluate it first
            if (rhs != null && rhs.Contains("->"))
            {
                var rhsParts = rhs.Split("->", StringSplitOptions.RemoveEmptyEntries);

                if (rhsParts.Length > 0)
                {
                    string firstRhs = rhsParts[0];

                    if (firstRhs == "this")
                    {
                        if (id is null)
                            throw new Exception("'this' reference outside the bounds of an object");

                        WriteLine($"{opcodeMap["PUSH"]} _ref({id})");
                    }
                    else if (firstRhs.StartsWith("_ref("))
                        WriteLine($"{opcodeMap["PUSH"]} {firstRhs}");
                    else if (aliasMap.TryGetValue(firstRhs, out int aliasID))
                        WriteLine($"{opcodeMap["PUSH"]} _ref({aliasID})");
                    else // assume value is a type literal
                        WriteLine($"{opcodeMap["PUSH"]} _type({firstRhs})");

                    for (int i = 1; i < rhsParts.Length; i++)
                    {
                        string part = rhsParts[i];
                        if (string.IsNullOrWhiteSpace(part))
                            continue;

                        if (part.Contains('(') && part.Contains(')'))
                        {
                            ParseMethodCall(id, part);
                        }
                        else
                        {
                            if (part.EndsWith(';'))
                                part = part[..^1];
                            WriteLine($"{opcodeMap["ACCESS"]} {part}");
                        }
                    }
                }
                else
                    throw new Exception($"nothing to access on the right side...");
            }

            // Step 3: Process LHS
            var lhsParts = accessPart.Split("->", StringSplitOptions.RemoveEmptyEntries);

            if (lhsParts.Length == 0)
                return;

            string first = lhsParts[0];

            if (lhsParts.Length is 1)
            {
                WriteLine($"{opcodeMap["SET"]} {first} _stack()");
                return;
            }

            if (first == "this")
            {
                if (id is null)
                    throw new Exception("'this' reference outside the bounds of an object");

                WriteLine($"{opcodeMap["PUSH"]} _ref({id})");
            }
            else if (first.StartsWith("_ref("))
                WriteLine($"{opcodeMap["PUSH"]} {first}");
            else if (aliasMap.TryGetValue(first, out int aliasID))
                WriteLine($"{opcodeMap["PUSH"]} _ref({aliasID})");
            else // assume value is a type literal
                WriteLine($"{opcodeMap["PUSH"]} _type({first})");

            for (int i = 1; i < lhsParts.Length; i++)
            {
                string part = lhsParts[i];
                if (string.IsNullOrWhiteSpace(part))
                    continue;

                bool isLast = i == lhsParts.Length - 1;

                if (part.Contains('(') && part.Contains(')'))
                    throw new Exception("Left hand side function is illegal.");
                else
                {
                    if (rhs != null && isLast)
                    {
                        string val = rhs.Contains("->") ? "_stack()" : rhs;
                        if (val.EndsWith(';'))
                            val = val[..^1];
                        WriteLine($"{opcodeMap["SETACCESS"]} {part} {val}");
                    }
                    else
                        WriteLine($"{opcodeMap["ACCESS"]} {part}");
                }
            }

            void ParseMethodCall(string? id, string part)
            {
                var openParen = part.IndexOf('(');
                var closeParen = part.LastIndexOf(')');

                var methodName = part[..openParen].Trim();
                var argList = part.Substring(openParen + 1, closeParen - openParen - 1);
                var args = argList.Split(',').Select(x => x.Trim()).ToList();

                for (int j = args.Count - 1; j >= 0; j--)
                {
                    string arg = args[j];

                    if (arg is "..")
                        continue; // assumed stack value exists from elsewhere

                    if (arg.Contains("->"))
                        HandleAccessing(id);
                    else
                        WriteLine($"{opcodeMap["PUSH"]} {arg}");
                }

                WriteLine($"{opcodeMap["CALL"]} {methodName} {args.Count}");
            }
        }

        private bool ParseList()
        {
            int typeOpen = this.currentLine!.IndexOf("<");
            int typeClose = this.currentLine.LastIndexOf(">");
            if (typeOpen == -1 || typeClose == -1 || typeClose < typeOpen)
                throw new Exception("Expected <TYPE1,TYPE2,...> to indicate the type(s) of the collection before [");

            string types = this.currentLine.Substring(typeOpen + 1, typeClose - typeOpen - 1).Trim();
            if (string.IsNullOrWhiteSpace(types))
                throw new Exception("Failed to parse types: " + this.currentLine);
            WriteLine($"{opcodeMap["LIST_START"]} " + types);

            bool insideFunction = false;
            StringBuilder currentElement = new();

            bool collectingDefinition = false;
            bool collectingString = false;
            int stringDepth = 0;
            char prefStringChar = '\0';
            int depth = 0;
            int listDepth = 1;
            char? currentChar;

            string currentLine = this.currentLine[(typeClose + 2)..];

            bool lastCharWasClose = false;
            do
            {
                foreach (char c in currentLine)
                {
                    int res = handleChar(ref insideFunction, currentElement, ref collectingDefinition, ref depth, ref listDepth, c, ref prefStringChar);
                    if (res == -1)
                        return true;
                }
                if (collectingDefinition)
                    currentElement.Append('\n');
            } while ((currentLine = ReadNonEmptyLine()) != null);

            throw new Exception("Expected ']' to close list.");

            void emitElement()
            {
                if (currentElement.Length > 0)
                {
                    string currentElementString = currentElement.ToString();

                    if (currentElementString.Contains(":"))
                    {
                        lineBuffers.Push(new OverridableStack<string>());
                        var lines = currentElementString.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in lines)
                            lineBuffers.Peek().PushEnd(line.Trim() + '\n');

                        this.currentLine = ReadNonEmptyLine();
                        int colonIndex = this.currentLine.IndexOf(':');
                        int braceIndex = this.currentLine.IndexOf('{');
                        string id;
                        if (braceIndex == -1)
                            id = this.currentLine[colonIndex..].Trim();
                        else
                            id = this.currentLine.Substring(colonIndex + 1, braceIndex - colonIndex - 1).Trim();
                        ParseObjectOrAssignment();
                        WriteLine($"{opcodeMap["ELEMENT"]} _ref({id})");
                    }
                    else
                    {
                        currentElementString = ValidateValue(currentElementString);
                        WriteLine($"{opcodeMap["ELEMENT"]} " + currentElementString);
                    }
                    currentElement.Clear();
                }
            }

            int handleChar(ref bool insideFunction, StringBuilder currentElement, ref bool collectingDefinition, ref int depth, ref int listDepth, char? currentChar, ref char prefStringChar)
            {
                char character = currentChar.Value;

                if (!collectingDefinition && char.IsWhiteSpace(character))
                    return 1;
                if (collectingString)
                {
                    currentElement.Append(character);
                    if (character == '"' && prefStringChar != '\\')
                    {
                            collectingString = false;
                        prefStringChar = '\0';
                        return 1;
                    }

                    prefStringChar = character;
                    return 1;
                }

                if (character == '"' && !collectingString)
                {
                    collectingString = true;
                    stringDepth = 1;
                    currentElement.Append(character);
                    return 1;
                }

                if (character == '{')
                {
                    collectingDefinition = true;
                    depth++;
                    currentElement.Append(character);
                    return 1;
                }

                if (character == '}')
                {
                    depth--;
                    currentElement.Append(character);

                    if (depth == 0)
                    {
                        collectingDefinition = false;
                        emitElement();
                        return 1;
                    }
                    return 1;
                }

                if (character == '(')
                {
                    insideFunction = true;
                    currentElement.Append(character);
                    return 1;
                }

                if (character == ')')
                {
                    insideFunction = false;
                    currentElement.Append(character);
                    return 1;
                }

                if (!insideFunction && character == ',')
                {
                    if (!collectingDefinition)
                        emitElement();
                    else
                        currentElement.Append(character);

                    return 1;
                }

                if (!insideFunction && character == '[')
                    listDepth++;

                if (!insideFunction && character == ']')
                {
                    listDepth--;
                    if (listDepth is not 0 || collectingDefinition)
                    {
                        currentElement.Append("\n]\n");
                        return 1;
                    }
                    emitElement();
                    WriteLine(opcodeMap["LIST_END"].ToString());
                    if (listDepth == 0)
                        return -1;
                    else return 1;
                }

                currentElement.Append(character);
                return 0;
            }

        }

        private void ParseAssignment(string line)
        {
            line = line.TrimEnd(';');
            int eq = line.IndexOf('=');
            string key = line[..eq].Trim();
            string value = ValidateValue(line[(eq + 1)..].Trim());

            WriteLine($"{opcodeMap["SET"]} {key} {value}");
        }

        private string ValidateValue(string value)
        {
            if (value.StartsWith('\"'))
            {
                string fullString = ReadString(value);

                if (!fullString.Contains('\n'))
                    return fullString;
                else
                {
                    writer.WriteLine(opcodeMap["START_STR"]);
                    foreach (string line in fullString.Split('\n'))
                        writer.WriteLine($"{opcodeMap["STR"]} \"{line}\"");
                    writer.WriteLine(opcodeMap["END_STR"]);

                    return "_stack()";
                }
            }
            return value;
        }

        private string ReadString(string start)
        {
            StringBuilder content = new();
            bool inEscape = false;
            bool isMultiline = false;
            bool inside = false;
            int quoteCount = 0;

            // Determine quote style (single vs 5x)
            for (int i = 0; i < start.Length; i++)
            {
                if (start[i] == '"')
                {
                    quoteCount++;
                    inside = true;
                }
                else if (!char.IsWhiteSpace(start[i]))
                {
                    break;
                }
            }

            if (!inside)
                throw new InvalidOperationException("String must start with at least one quote.");

            if (quoteCount == 1)
                isMultiline = false;
            else if (quoteCount == 5)
                isMultiline = true;
            else
                throw new InvalidOperationException("Invalid number of quotes to start string. Use 1 or 5.");

            int startOffset = quoteCount;

            for (int i = startOffset; i < start.Length; i++)
            {
                char c = start[i];

                if (inEscape)
                {
                    content.Append(c switch
                    {
                        '"' => '"',
                        '\\' => '\\',
                        'n' => '\n',
                        't' => '\t',
                        _ => '\\' + c.ToString()
                    });

                    inEscape = false;
                }
                else if (c == '\\')
                {
                    inEscape = true;
                }
                else if (c == '"')
                {
                    // check ahead to see if we hit the closing quote sequence
                    int remaining = start.Length - i;
                    if (isMultiline && remaining >= 5 && start.Substring(i, 5) == "\"\"\"\"\"")
                    {
                        return content.ToString();
                    }
                    else if (!isMultiline)
                    {
                        return content.ToString();
                    }
                    else
                    {
                        content.Append('"');
                    }
                }
                else
                {
                    content.Append(c);
                }
            }

            if (!isMultiline)
                throw new InvalidOperationException("Malformed multiline string: unexpected line break in single-quote string.");

            // Keep reading until we find the closing 5x quote
            while (true)
            {
                string? nextLine = ReadNonEmptyLine(allowEmptyLines: true);
                if (nextLine == null)
                    throw new InvalidOperationException("Unexpected end of stream while reading multiline string.");

                content.Append('\n');

                for (int i = 0; i < nextLine.Length; i++)
                {
                    char c = nextLine[i];

                    if (inEscape)
                    {
                        content.Append(c switch
                        {
                            '"' => '"',
                            '\\' => '\\',
                            'n' => '\n',
                            't' => '\t',
                            _ => '\\' + c.ToString()
                        });

                        inEscape = false;
                    }
                    else if (c == '\\')
                    {
                        inEscape = true;
                    }
                    else if (c == '"')
                    {
                        // lookahead for 5x quote
                        if (i + 4 < nextLine.Length && nextLine.Substring(i, 5) == "\"\"\"\"\"")
                        {
                            return content.ToString();
                        }
                        else
                        {
                            content.Append('"');
                        }
                    }
                    else
                    {
                        content.Append(c);
                    }
                }
            }
        }

        private string? ReadNonEmptyLine(bool allowEmptyLines = false)
        {
            string? line;
            do
            {
                if (lineBuffers.Count > 0)
                {
                    if ((lineBuffers.Peek()?.Count ?? 0) > 0)
                    {
                        line = lineBuffers.Peek().Pop();
                        if (lineBuffers.Peek().Count == 0)
                            lineBuffers.Pop();
                    }
                    else
                    {
                        lineBuffers.Pop();
                        return null;
                    }
                }
                else
                {
                    if (reader.EndOfStream)
                        return null;
                    line = reader.ReadLine();
                }

                if (allowEmptyLines)
                    return line;

            } while (string.IsNullOrWhiteSpace(line));

            return line;
        }

        private void ReadNextLineExpecting(string expected)
        {
            currentLine = ReadNonEmptyLine();
            if (currentLine == null || currentLine.Trim() != expected)
                throw new Exception($"Expected '{expected}' but got: {currentLine}");
        }

        private void WriteLine(string line)
        {
            writer.WriteLine(line);
        }
    }

}
