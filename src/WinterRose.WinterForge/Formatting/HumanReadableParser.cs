using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WinterRose.Reflection;
using WinterRose.WinterForgeSerializing.Instructions;
using WinterRose.WinterForgeSerializing.Util;
using WinterRose.WinterForgeSerializing;
using WinterRose.WinterForgeSerializing.Formatting;
using WinterRose.WinterForgeSerializing.Workers;
using WinterRose.WinterForgeSerializing.Expressions;

namespace WinterRose.WinterForgeSerializing.Formatting
{
    public class HumanReadableParser
    {
        private enum CollectionParseResult
        {
            Failed,
            NotACollection,
            ListOrArray,
            Dictionary
        }

        private StreamReader reader = null!;
        private StreamWriter writer = null!;
        internal string? currentLine;
        private int depth = 0;
        private Dictionary<string, int> aliasMap = [];
        private readonly Stack<OverridableStack<string>> lineBuffers = new();
        List<int> foundIds = [];
        List<string> variables = [];
        private int autoAsIDs;
        private static readonly Dictionary<OpCode, int> opcodeMap = Enum
            .GetValues<OpCode>()
            .ToDictionary(op => op, op => (int)op);

        int ldI = 0;
        int ldD = 0;
        private List<(string start, string end)> flowLabels = [];

        //private static readonly Dictionary<string, string> opcodeMap = Enum
        //  .GetValues<OpCode>()
        //  .ToDictionary(op => op.ToString(), op => op.ToString());

        /// <summary>
        /// Parses the human readable format of WinterForge into the opcodes that the <see cref="InstructionParser"/> understands. so that the <see cref="WinterForgeVM"/> can deserialize
        /// </summary>
        /// <param name="input">The source of human readable format</param>
        /// <param name="output">The destination where the WinterForge opcodes will end up</param>
        /// <remarks>Appends a line 'WF_ENDOFDATA' when <paramref name="output"/> is of type <see cref="NetworkStream"/></remarks>
        public void Parse(Stream input, Stream output)
        {
            using var _ = reader = new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            using var _1 = writer = new StreamWriter(output, Encoding.UTF8, leaveOpen: true);

            string version = typeof(WinterForge)
                .Assembly!
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
                .InformationalVersion;

            WriteLine($"// Parsed by WinterForge {version.Split('+')[0]}");
            WriteLine("");
            WriteLine("");

            while ((currentLine = ReadLine()) != null)
                ParseObjectOrAssignment(false);

            if (output is NetworkStream)
                writer.WriteLine("WF_ENDOFDATA");
            writer.Flush();
        }

        internal void ContinueWithBlock(string? id, bool asBody, string? line = null)
        {
            currentLine = line ??= ReadLine()?.Trim();
            if (line is null)
                return;
            do
            {
                if (currentLine == "}")
                    break;
                ParseBlockLine(id, asBody, currentLine);
            }
            while ((currentLine = ReadLine()?.Trim()) != null);
        }

        internal void ParseObjectOrAssignment(bool isBody)
        {
            string line = currentLine!.Trim();

            if (line.Trim().StartsWith("//"))
                return;

            if (TryParseFirstParts())
                return;
            if (line.EndsWith(':') && !line.Contains('='))
            {
                WriteLine($"{opcodeMap[OpCode.LABEL]} {line.Trim()[..^1]}");
            }
            else if (line.StartsWith("goto "))
            {
                if (line.EndsWith(';'))
                    line = line[..^1];
                WriteLine($"{opcodeMap[OpCode.JUMP]} {line["goto".Length..].Trim()}");
            }
            // Constructor Definition: Type(arguments) : ID {
            else if (line.Contains('(') && line.Contains(')') && ContainsSequenceOutsideQuotes(line, ":") != -1 && line.Contains('{'))
            {
                int openParenIndex = line.IndexOf('(');
                int closeParenIndex = line.IndexOf(')');
                int colonIndex = line.IndexOf(':');
                int braceIndex = line.IndexOf('{');

                string type = line[..openParenIndex].Trim();
                string arguments = line.Substring(openParenIndex + 1, closeParenIndex - openParenIndex - 1).Trim();
                string idRaw = line.Substring(colonIndex + 1, braceIndex - colonIndex - 1).Trim();

                // special keywords -> numeric id
                if (idRaw == "temp" || idRaw == "nextid")
                    idRaw = GetAutoID().ToString();

                if (!int.TryParse(idRaw, out int idNum))
                {
                    int assignedId = GetAutoID();
                    foundIds.Add(assignedId);

                    var args = arguments.Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    foreach (string arg in args)
                        WriteLine($"{opcodeMap[OpCode.PUSH]} " + arg);
                    WriteLine($"{opcodeMap[OpCode.DEFINE]} {type} {assignedId} {args.Length}");
                    depth++;

                    ParseBlock(assignedId.ToString(), isBody);

                    bool isGlobal = false;
                    if (idRaw.StartsWith("global"))
                    {
                        idRaw = idRaw["global".Length..].Trim();
                        isGlobal = true;
                    }

                    string varName = idRaw;
                    if (!isGlobal)
                    {
                        WriteLine($"{opcodeMap[OpCode.FORCE_DEF_VAR]} {varName}");
                        WriteLine($"{opcodeMap[OpCode.SET]} {varName} #ref({assignedId})");
                        variables.Add(varName);
                    }
                    else
                    {
                        aliasMap.Add(varName, assignedId);
                    }
                }
                else
                {
                    foundIds.Add(idNum);

                    var args = arguments.Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    foreach (string arg in args)
                        WriteLine($"{opcodeMap[OpCode.PUSH]} " + arg);
                    WriteLine($"{opcodeMap[OpCode.DEFINE]} {type} {idNum} {args.Length}");
                    depth++;
                    ParseBlock(idNum.ToString(), isBody);
                }
            }
            // Constructor Definition with no block: Type(arguments) : ID;
            else if (line.Contains('(') && line.Contains(')') && ContainsSequenceOutsideQuotes(line, ":") != -1 && line.EndsWith(";"))
            {
                int openParenIndex = line.IndexOf('(');
                int closeParenIndex = line.IndexOf(')');
                int colonIndex = line.IndexOf(':');

                string type = line[..openParenIndex].Trim();
                if (type.Contains("Anonymous"))
                    type = type.Replace(' ', '-');

                string arguments = line.Substring(openParenIndex + 1, closeParenIndex - openParenIndex - 1).Trim();
                string idRaw = line.Substring(colonIndex + 1, line.Length - colonIndex - 2).Trim();

                if (idRaw == "temp" || idRaw == "nextid")
                    idRaw = GetAutoID().ToString();

                if (!int.TryParse(idRaw, out int idNum))
                {
                    int assignedId = GetAutoID();
                    foundIds.Add(assignedId);

                    var args = arguments.Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    foreach (string arg in args)
                        WriteLine($"{opcodeMap[OpCode.PUSH]} " + arg);
                    WriteLine($"{opcodeMap[OpCode.DEFINE]} {type} {assignedId} {args.Length}");
                    WriteLine($"{opcodeMap[OpCode.END]} {assignedId}");

                    bool isGlobal = false;
                    if (idRaw.StartsWith("global"))
                    {
                        idRaw = idRaw["global".Length..].Trim();
                        isGlobal = true;
                    }

                    string varName = idRaw;
                    if (!isGlobal)
                    {
                        WriteLine($"{opcodeMap[OpCode.FORCE_DEF_VAR]} {varName}");
                        WriteLine($"{opcodeMap[OpCode.SET]} {varName} #ref({assignedId})");
                        variables.Add(varName);
                    }
                    else
                    {
                        aliasMap.Add(varName, assignedId);
                    }
                }
                else
                {
                    // numeric id path (unchanged)
                    foundIds.Add(idNum);

                    var args = arguments.Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    foreach (string arg in args)
                        WriteLine($"{opcodeMap[OpCode.PUSH]} " + arg);
                    WriteLine($"{opcodeMap[OpCode.DEFINE]} {type} {idNum} {args.Length}");
                    WriteLine($"{opcodeMap[OpCode.END]} {idNum}");
                }
            }
            // Definition: Type : ID {
            else if (ContainsSequenceOutsideQuotes(line, ":") != -1 && line.Contains('{'))
            {
                int colonIndex = line.IndexOf(':');
                int braceIndex = line.IndexOf('{');

                string type = line[..colonIndex].Trim();
                if (type.Contains("Anonymous"))
                    type = type.Replace(' ', '-');

                string idRaw = line.Substring(colonIndex + 1, braceIndex - colonIndex - 1).Trim();

                if (idRaw == "temp" || idRaw == "nextid")
                    idRaw = GetAutoID().ToString();

                if (!int.TryParse(idRaw, out int idNum))
                {
                    int assignedId = GetAutoID();
                    foundIds.Add(assignedId);

                    WriteLine($"{opcodeMap[OpCode.DEFINE]} {type} {assignedId} 0");
                    depth++;
                    ParseBlock(assignedId.ToString(), isBody);

                    bool isGlobal = false;
                    if (idRaw.StartsWith("global"))
                    {
                        idRaw = idRaw["global".Length..].Trim();
                        isGlobal = true;
                    }

                    string varName = idRaw;
                    if (!isGlobal)
                    {
                        WriteLine($"{opcodeMap[OpCode.FORCE_DEF_VAR]} {varName}");
                        WriteLine($"{opcodeMap[OpCode.SET]} {varName} #ref({assignedId})");
                        variables.Add(varName);
                    }
                    else
                    {
                        aliasMap.Add(varName, assignedId);
                    }
                }
                else
                {
                    foundIds.Add(idNum);
                    WriteLine($"{opcodeMap[OpCode.DEFINE]} {type} {idNum} 0");
                    depth++;
                    ParseBlock(idNum.ToString(), isBody);
                }
            }
            // Definition: Type : ID;
            else if (ContainsSequenceOutsideQuotes(line, ":") != -1 && line.EndsWith(';'))
            {
                string type;
                string idRaw;

                var parts = line[..^1].Split(':');
                type = parts[0].Trim();
                if (type.Contains("Anonymous"))
                    type = type.Replace(' ', '-');
                idRaw = parts[1].Trim();

                if (idRaw == "temp" || idRaw == "nextid")
                    idRaw = GetAutoID().ToString();

                if (!int.TryParse(idRaw, out int idNum))
                {
                    int assignedId = GetAutoID();
                    foundIds.Add(assignedId);

                    WriteLine($"{opcodeMap[OpCode.DEFINE]} {type} {assignedId} 0");
                    WriteLine($"{opcodeMap[OpCode.END]} {assignedId}");

                    bool isGlobal = false;
                    if (idRaw.StartsWith("global"))
                    {
                        idRaw = idRaw["global".Length..].Trim();
                        isGlobal = true;
                    }

                    string varName = idRaw;
                    if (!isGlobal)
                    {
                        WriteLine($"{opcodeMap[OpCode.FORCE_DEF_VAR]} {varName}");
                        WriteLine($"{opcodeMap[OpCode.SET]} {varName} #ref({assignedId})");
                        variables.Add(varName);
                    }
                    else
                    {
                        aliasMap.Add(varName, assignedId);
                    }
                }
                else
                {
                    foundIds.Add(idNum);

                    WriteLine($"{opcodeMap[OpCode.DEFINE]} {type} {idNum} 0");
                    WriteLine($"{opcodeMap[OpCode.END]} {idNum}");
                }
            }
            // Definition: Type : ID
            else if (ContainsSequenceOutsideQuotes(line, ":") != -1)
            {
                string type;
                string idRaw;

                var parts = line.Split(':');
                type = parts[0].Trim();
                if (type.Contains("Anonymous"))
                    type = type.Replace(' ', '-');
                idRaw = parts[1].Trim();

                // allow 'temp' shorthand
                if (idRaw == "temp" || idRaw == "nextid")
                    idRaw = GetAutoID().ToString();

                if (!int.TryParse(idRaw, out int idNum))
                {
                    int assignedId = GetAutoID();
                    foundIds.Add(assignedId);

                    ReadNextLineExpecting("{");

                    WriteLine($"{opcodeMap[OpCode.DEFINE]} {type} {assignedId} 0");
                    depth++;

                    ParseBlock(assignedId.ToString(), isBody);

                    bool isGlobal = false;
                    if (idRaw.StartsWith("global"))
                    {
                        idRaw = idRaw["global".Length..].Trim();
                        isGlobal = true;
                    }

                    string varName = idRaw;
                    if (!isGlobal)
                    {
                        WriteLine($"{opcodeMap[OpCode.FORCE_DEF_VAR]} {varName}");
                        WriteLine($"{opcodeMap[OpCode.SET]} {varName} #ref({assignedId})");
                        variables.Add(varName);
                    }
                    else
                        aliasMap.Add(varName, assignedId);
                }
                else
                {
                    foundIds.Add(idNum);
                    ReadNextLineExpecting("{");

                    WriteLine($"{opcodeMap[OpCode.DEFINE]} {type} {idNum} 0");
                    depth++;

                    ParseBlock(idNum.ToString(), isBody);
                }
            }
            else if (line.StartsWith("var ") && ContainsSequenceOutsideQuotes(line, "=") is int eqI)
            {
                ParseVarCreation(isBody, line, eqI);
            }
            else if (line.StartsWith("global ") && ContainsSequenceOutsideQuotes(line, "=") is int eqI2)
            {
                ParseGlobalVarCreation(isBody, line, eqI2);
            }
            else if (line.StartsWith("return"))
            {
                HandleReturn(line, isBody, null);
            }
            else if (line.Contains("->"))
            {
                HandleAccessing(null, isBody);
            }
            else if (line.StartsWith("if "))
            {
                ParseIfChain(line, isBody);
            }
            else if (!string.IsNullOrWhiteSpace(line) &&
                    line.EndsWith(":") &&
                    line.IndexOf(':') == line.Length - 1 &&
                    line[..^1].All(c => char.IsLetterOrDigit(c) || c == '_'))
            {
                string name = line[..^1];
                WriteLine($"{opcodeMap[OpCode.LABEL]} {name}");
            }
            else if (line.StartsWith("as"))
            {
                string id = line[2..].Trim();
                if (id.EndsWith(';'))
                    id = id[..^1];

                if (id is "nextid")
                    id = GetAutoID().ToString();

                WriteLine($"{opcodeMap[OpCode.AS]} {id}");
                foundIds.Add(int.Parse(id));
            }
            else if (HasValidGenericFollowedByBracket(line))
            {
                ParseCollection(isBody);
            }
            else if (line.StartsWith("alias"))
            {
                string[] parts = line.Split(' ');
                int id = int.Parse(parts[1]);
                if (parts[2] is "as" && parts.Length > 2)
                    parts[2] = parts[3];
                string alias = parts[2].EndsWith(';') ? parts[2][..^1] : parts[2];
                aliasMap.Add(alias, id);
            }
            else if (line.StartsWith("#container"))
            {
                ContainerParser.ParseContainers(this, writer);
            }
            else if (line.StartsWith("#template "))
            {
                line = line["#template".Length..].Trim();
                ContainerParser.ParseContainers(this, writer);
            }
            else if (line.StartsWith("while"))
            {
                ParseWhileLoop(null, isBody);
            }
            else if (line.StartsWith("for "))
            {
                ParseForLoop(null, isBody);
            }
            else if (ContainsSequenceOutsideQuotes(line, "(") != -1 && EndsWithParenOrParenSemicolon(line))
            {
                ParseMethodCall(null, line, isBody);
                WriteLine($"{opcodeMap[OpCode.VOID_STACK_ITEM]}");
            }
            else
                throw new Exception($"Unexpected top-level line: {line}");
        }

        private void ParseGlobalVarCreation(bool isBody, string line, int eqI2)
        {
            string varName = line["global".Length..eqI2].Trim();
            string rhs = line[(eqI2 + 1)..].Trim();
            if (rhs.EndsWith(';'))
                rhs = rhs[..^1];
            int nextid = GetAutoID();
            string tempVar = ValidateValue(rhs, isBody);
            if (tempVar is not "#stack()")
                WriteLine($"{opcodeMap[OpCode.PUSH]} {tempVar}");
            WriteLine($"{opcodeMap[OpCode.AS]} {nextid}");
            foundIds.Add(nextid);
            aliasMap.Add(varName, nextid);
        }

        private void ParseVarCreation(bool isBody, string line, int eqI)
        {
            string varName = line[4..eqI].Trim();
            string rhs = line[(eqI + 1)..].Trim();
            if (rhs.EndsWith(';'))
                rhs = rhs[..^1];
            string tempVar = ValidateValue(rhs, isBody);
            WriteLine($"{opcodeMap[OpCode.FORCE_DEF_VAR]} {varName}");
            WriteLine($"{opcodeMap[OpCode.SET]} {varName} {tempVar}");
        }

        public static bool EndsWithParenOrParenSemicolon(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            int len = input.Length;

            // Check last char
            if (input[len - 1] == ')')
                return true;

            // Check last two chars
            if (len > 1 && input[len - 2] == ')' && input[len - 1] == ';')
                return true;

            return false;
        }

        private void ParseIfChain(string? id, bool isBody)
        {
            string endLabel = "L" + GetAutoID();

            while (true)
            {
                string raw = currentLine.Trim();

                bool isIf = raw.StartsWith("if ");
                bool isElseIf = raw.StartsWith("else if ");
                bool isElse = raw == "else" || raw.StartsWith("else ");

                if (!isIf && !isElseIf && !isElse)
                    throw new WinterForgeFormatException("ParseIfChain called when current line is not an if/else-if/else");

                string conditionExpr = null;
                bool hasCondition = false;

                if (isIf || isElseIf)
                {
                    int kwLen = isIf ? 2 : 7;
                    int braceIndex = raw.IndexOf('{');

                    if (braceIndex >= 0)
                        conditionExpr = raw[kwLen..braceIndex].Trim();
                    else
                        conditionExpr = raw[kwLen..].Trim();

                    hasCondition = !string.IsNullOrEmpty(conditionExpr);
                    if (!hasCondition)
                        throw new WinterForgeFormatException("Missing condition on if/else if");
                }

                string nextBranchLabel = "L" + GetAutoID();
                if (hasCondition)
                {
                    string condToken = ValidateValue(conditionExpr, isBody, id);
                    if (condToken is not "#stack()")
                        WriteLine($"{opcodeMap[OpCode.PUSH]} {condToken}");
                    WriteLine($"{opcodeMap[OpCode.JUMP_IF_FALSE]} {nextBranchLabel}");
                }

                if (!raw.Contains('{'))
                    raw = currentLine = ReadLine()?.Trim();

                WriteLine($"{opcodeMap[OpCode.SCOPE_PUSH]}");

                if (currentLine == null)
                    throw new WinterForgeFormatException("Unexpected EOF in if statement body");

                if (raw.Contains('{'))
                {
                    ContinueWithBlock(id, isBody);
                }
                else
                {
                    ParseBlockLine(id, isBody, currentLine);
                }

                WriteLine($"{opcodeMap[OpCode.SCOPE_POP]}");

                if (hasCondition)
                    WriteLine($"{opcodeMap[OpCode.JUMP]} {endLabel}");

                WriteLine($"{opcodeMap[OpCode.LABEL]} {nextBranchLabel}");

                string next = PeekNonEmptyLine()?.Trim() ?? "";
                if (next.StartsWith("else if ") || next == "else" || next.StartsWith("else "))
                {

                    currentLine = ReadLine();
                    continue;
                }
                else
                {
                    WriteLine($"{opcodeMap[OpCode.LABEL]} {endLabel}");
                    break;
                }
            }
        }

        private void HandleReturn(string line, bool isBody, string? id)
        {
            int trimoffEnd = line.EndsWith(';') ? 1 : 0;
            string rawID = line[6..^trimoffEnd].Trim();

            if (ContainsSequenceOutsideQuotes(rawID, " ") != -1)
            {
                string name = UniqueRandomVarNameGenerator.Next;
                WriteLine($"{opcodeMap[OpCode.FORCE_DEF_VAR]} {name}");
                ParseAssignment($"{name} = {rawID}", null, isBody);
                WriteLine($"{opcodeMap[OpCode.RET]} {name}");
                return;
            }

            if (!isBody)
            {
                if (string.IsNullOrWhiteSpace(rawID) ||
                    (!rawID.All(char.IsDigit) && rawID != "#stack()" && rawID != "null"))
                {
                    if (aliasMap.TryGetValue(rawID, out int aliasID))
                        rawID = aliasID.ToString();
                }

                if (rawID.All(char.IsDigit))
                    rawID = $"#ref({rawID})";
                if (ContainsSequenceOutsideQuotes(rawID, "->") != -1)
                {
                    HandleAccessing(id, isBody, rawID, true);
                    rawID = "#stack()";
                }
                string result = $"{opcodeMap[OpCode.RET]} {rawID}";
                WriteLine(result);
            }
            else
            {
                rawID = ValidateValue(rawID, isBody, id);
                string result = $"{opcodeMap[OpCode.RET]} {rawID}";
                WriteLine(result);
            }
        }

        private bool TryParseFirstParts()
        {
            if (currentLine.StartsWith("#import", StringComparison.OrdinalIgnoreCase))
            {
                string line = currentLine[7..].Trim();
                string importedFileName = ReadString(line)[1..^1];
                line = line[(importedFileName.Length + 2)..].Trim();
                int id = GetAutoID();
                if (line.StartsWith("as"))
                {
                    line = line[2..].Trim();

                    int aliasEndIndex = line.IndexOf(' ');
                    if (aliasEndIndex is -1)
                        aliasEndIndex = line.IndexOf(';');
                    if (aliasEndIndex is -1)
                        aliasEndIndex = line.Length;

                    string alias = line[..aliasEndIndex].Trim();
                    aliasMap[alias] = id;
                    line = line[alias.Length..].Trim();
                }

                if (line.StartsWith("(compiles into "))
                {
                    if (!line.EndsWith(')'))
                        throw new WinterForgeFormatException("Import compile statement not closed with )");
                    // compile imported file
                    line = line[15..^1];

                    WinterForge.ConvertFromFileToFile(importedFileName, line);
                    importedFileName = line;
                }

                WriteLine($"{opcodeMap[OpCode.IMPORT]} \"{importedFileName}\" {id}");
                return true;
            }
            return false;
        }
        private static bool HasValidGenericFollowedByBracket(ReadOnlySpan<char> input)
        {
            int newlineIndex = input.IndexOf('\n');
            ReadOnlySpan<char> firstLine = newlineIndex == -1 ? input : input[..newlineIndex];

            int length = firstLine.Length;
            int i = 0;

            // Find first '<' in the first line
            while (i < length && firstLine[i] != '<') i++;
            if (i == length) return false;

            int depth = 0;
            for (; i < length; i++)
            {
                char c = firstLine[i];
                if (c == '<')
                    depth++;
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
        private void ParseBlock(string id, bool isBody)
        {
            if (id is "1")
                ;
            while ((currentLine = ReadLine()) != null)
            {
                string line = currentLine.Trim();
                bool? flowControl = ParseBlockLine(id, isBody, line);
                switch (flowControl)
                {
                    case false: continue;
                    case true: return;
                }
            }
        }

        private void ParseWhileLoop(string? id, bool isBody)
        {
            string labelStart = "WHILE" + GetAutoID();
            string labelEnd = "WHILE" + GetAutoID();
            string expr = currentLine["while".Length..].Trim();

            flowLabels.Add((labelStart, labelEnd));

            WriteLine($"{opcodeMap[OpCode.LABEL]} {labelStart}");
            // emits opcodes to compute the expression, hence why start label comes first so this is re-evaluated each iteration
            expr = ValidateValue(expr, isBody, id);
            WriteLine($"{opcodeMap[OpCode.JUMP_IF_FALSE]} {labelEnd}");
            WriteLine($"{opcodeMap[OpCode.SCOPE_PUSH]}");

            currentLine = ReadLine();
            if ((currentLine ?? throw new WinterForgeFormatException("End of file not expected in 'while' definition")).Trim() != "{")
                ParseBlockLine(id, isBody, currentLine);
            else
                ContinueWithBlock(id, isBody);

            WriteLine($"{opcodeMap[OpCode.SCOPE_POP]}");
            WriteLine($"{opcodeMap[OpCode.JUMP]} {labelStart}");
            WriteLine($"{opcodeMap[OpCode.LABEL]} {labelEnd}");

        }

        private void ParseForLoop(string? id, bool isBody)
        {
            string labelStart = "FOR" + GetAutoID();
            string labelEnd = "FOR" + GetAutoID();
            string[] expressions = SplitForLoopContent(currentLine["for".Length..].Trim());

            flowLabels.Add((labelStart, labelEnd));

            WriteLine($"{opcodeMap[OpCode.SCOPE_PUSH]}");

            if (!string.IsNullOrWhiteSpace(expressions[0]))
                ParseBlockLine(id, isBody, expressions[0]);

            WriteLine($"{opcodeMap[OpCode.LABEL]} {labelStart}");
            if (string.IsNullOrWhiteSpace(expressions[1]))
                throw new WinterForgeFormatException("For loop requires condition expression, initializer and iteration expression can be empty. eg: for ; true;");

            // emits opcodes to compute the expression, hence why start label comes first so this is re-evaluated each iteration
            ValidateValue(expressions[1], isBody, id);
            WriteLine($"{opcodeMap[OpCode.JUMP_IF_FALSE]} {labelEnd}");

            currentLine = ReadLine();
            if ((currentLine ?? throw new WinterForgeFormatException("End of file not expected in 'while' definition")).Trim() != "{")
                ParseBlockLine(id, isBody, currentLine);
            else
                ContinueWithBlock(id, isBody);

            if (!string.IsNullOrWhiteSpace(expressions[2]))
                ParseBlockLine(id, isBody, expressions[2]); // iterator expression

            WriteLine($"{opcodeMap[OpCode.JUMP]} {labelStart}");
            WriteLine($"{opcodeMap[OpCode.LABEL]} {labelEnd}");
            WriteLine($"{opcodeMap[OpCode.SCOPE_POP]}");
        }

        public static string[] SplitForLoopContent(string loopContent)
        {
            string[] parts = new string[3] { "", "", "" };
            int lastIndex = 0;
            int semicolonCount = 0;

            for (int i = 0; i < loopContent.Length; i++)
            {
                if (loopContent[i] == ';')
                {
                    parts[semicolonCount] = loopContent[lastIndex..(i + 1)].Trim();
                    lastIndex = i + 1;
                    semicolonCount++;

                    if (semicolonCount == 2)
                        break;
                }
            }

            if (parts[1] is ";")
                parts[1] = "";

            parts[2] = loopContent[lastIndex..].Trim();
            if (string.IsNullOrWhiteSpace(parts[1]) && !string.IsNullOrWhiteSpace(parts[2]) && parts[2] is not ";" and { Length: > 1})
            {
                (parts[1], parts[2]) = (parts[2], parts[1]);
            }
            if (parts[2].Length > 1 && !parts[2].EndsWith(';'))
                parts[2] += ';';
            if (parts[2] is ";")
                parts[2] = "";

            return parts;
        }

        /// <summary>
        /// Returns True when end of block is reached
        /// </summary>
        /// <param name="id"></param>
        /// <param name="isBody"></param>
        /// <param name="line"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        internal bool? ParseBlockLine(string id, bool isBody, string line)
        {
            if (line.Trim().StartsWith("//"))
                return false;

            if (line == "}")
            {
                depth--;

                WriteLine($"{opcodeMap[OpCode.END]} {id}");
                return true;
            }
            else if (line.EndsWith(':') && !line.Contains('='))
            {
                WriteLine($"{opcodeMap[OpCode.LABEL]} {line.Trim()[..^1]}");
            }
            else if (line.StartsWith("goto "))
            {
                if (line.EndsWith(';'))
                    line = line[..^1];
                WriteLine($"{opcodeMap[OpCode.JUMP]} {line["goto".Length..].Trim()}");
            }
            else if (isBody && currentLine.StartsWith("return"))
            {
                HandleReturn(currentLine, isBody, id);
            }
            else if (line.StartsWith("if "))
            {
                ParseIfChain(line, isBody);
            }
            else if (line.StartsWith("var ") && ContainsSequenceOutsideQuotes(line, "=") is int eqI)
            {
                ParseVarCreation(isBody, line, eqI);
            }
            else if (line.StartsWith("global ") && ContainsSequenceOutsideQuotes(line, "=") is int eqI2)
            {
                ParseGlobalVarCreation(isBody, line, eqI2);
            }
            else if (ContainsExpressionOutsideQuotes(line) && line.Contains(" = ") && line.EndsWith(';'))
                ParseAssignment(line, id, isBody);
            else if (line.Contains("->"))
                HandleAccessing(id, isBody);
            else if (line.IndexOf(':') is int colinx && line.IndexOf('=') is int eqinx
                && colinx is not -1 && eqinx is not -1
                && colinx < eqinx)
                ParseAnonymousAssignment(line, isBody);
            else if (line.Contains('=') && line.EndsWith(';'))
                ParseAssignment(line, id, isBody);
            else if (line.Contains('=') && line.Contains('\"'))
                ParseAssignment(line, id, isBody);
            else if (line.Contains(':'))
            {
                currentLine = line;
                ParseObjectOrAssignment(isBody); // nested define
            }
            else if (!string.IsNullOrWhiteSpace(line) &&
                    line.EndsWith(":") &&
                    line.IndexOf(':') == line.Length - 1 &&
                    line[..^1].All(c => char.IsLetterOrDigit(c) || c == '_'))
            {
                string name = line[..^1];
                WriteLine($"{opcodeMap[OpCode.LABEL]} {name}");
            }
            else if (line.StartsWith("as"))
            {
                string asid = line[2..].Trim();
                if (asid.EndsWith(';'))
                    asid = asid[..^1];
                WriteLine($"{opcodeMap[OpCode.AS]} {asid}");
            }
            else if (line.StartsWith("alias"))
            {
                string[] parts = line.Split(' ');
                int aliasid = int.Parse(parts[0]);
                WriteLine($"{opcodeMap[OpCode.ALIAS]} {aliasid} {parts[1]}");
            }
            else if (line.StartsWith("#template "))
            {
                line = line["#template".Length..].Trim();
                ContainerParser.ParseContainers(this, writer);
            }
            else if (line.StartsWith("#container"))
            {
                ContainerParser.ParseContainers(this, writer);
            }
            else if (line.StartsWith("continue") && flowLabels.Count > 0)
            {
                if (line.EndsWith(';'))
                    line = line[..^1];

                string countstr = line["continue".Length..].Trim();
                int count;
                if (!int.TryParse(countstr, out count))
                    count = 1;

                WriteLine($"{opcodeMap[OpCode.JUMP]} {flowLabels[^count].start}");
            }
            else if (line.StartsWith("break") && flowLabels.Count > 0)
            {
                if (line.EndsWith(';'))
                    line = line[..^1];

                string countstr = line["break".Length..].Trim();
                int count;
                if (!int.TryParse(countstr, out count))
                    count = 1;

                WriteLine($"{opcodeMap[OpCode.JUMP]} {flowLabels[^count].end}");
            }
            else if (line.StartsWith("for "))
            {
                ParseForLoop(id, isBody);
            }
            else if (TryParseCollection(line, out _, isBody) is var colres && colres is not CollectionParseResult.Failed and not CollectionParseResult.NotACollection)
            {
                return false;
            }
            else
            {
                throw new Exception($"unexpected block content: {line}");
            }

            return null;
        }

        private CollectionParseResult TryParseCollection(string line, out string name, bool isBody)
        {
            if (!line.Contains('['))
            {
                name = line;
                return CollectionParseResult.NotACollection;
            }
            name = "#stack()";

            currentLine = line;
            CollectionParseResult result = ParseCollection(isBody);

            int colonIndex = line.IndexOf(':');
            if (colonIndex is -1)
                colonIndex = line.IndexOf('[');
            int equalsIndex = line.IndexOf('=');
            if (equalsIndex != -1 && colonIndex > equalsIndex)
            {
                name = line[..equalsIndex].Trim();
                if (!string.IsNullOrEmpty(name))
                    WriteLine($"{opcodeMap[OpCode.SET]} {name} #stack()");
            }

            if (lineBuffers.Count > 0)
            {
                if (lineBuffers.Peek().Count >= 1)
                {
                    string last = lineBuffers.Peek().PeekLast();
                    if (last.Trim().EndsWith('}'))
                        return result; // Successfully parsed list and reached closing block
                }

                return CollectionParseResult.Failed; // Incomplete parse or something else to handle
            }

            return result;
        }
        private void ParseAnonymousAssignment(string line, bool isBody)
        {
            // Example: "type:name = value"
            int colonIndex = line.IndexOf('=');
            if (colonIndex == -1)
                throw new Exception("Invalid anonymous assignment format, missing ':'");
            string typeAndName = line[..colonIndex].Trim();
            string value = line[(colonIndex + 1)..].Trim();

            TryParseCollection(value, out value, isBody);

            if (value.EndsWith(';'))
                value = value[..^1].Trim();
            if (string.IsNullOrWhiteSpace(typeAndName) || string.IsNullOrWhiteSpace(value))
                throw new Exception("Invalid anonymous assignment format, type or name is empty");
            string[] typeAndNameParts = typeAndName.Split(':', 2, StringSplitOptions.TrimEntries);
            if (typeAndNameParts.Length != 2)
                throw new Exception("Invalid anonymous assignment format, expected 'type:name'");
            string type = typeAndNameParts[0];
            string name = typeAndNameParts[1];

            WriteLine($"{opcodeMap[OpCode.ANONYMOUS_SET]} {type} {name} {value}");
        }

        internal int ParseRHSAccess(string rhs, string? id, bool isBody)
        {
            var rhsParts = SplitPreserveParentheses(rhs);

            if (rhsParts.Count > 0)
            {
                string firstRhs = rhsParts[0];

                if (firstRhs == "this")
                {
                    if (id is null)
                        throw new WinterForgeFormatException("'this' reference outside the bounds of an object");

                    WriteLine($"{opcodeMap[OpCode.PUSH]} #ref({id})");
                }
                else if (firstRhs.StartsWith("#ref(") || variables.Contains(firstRhs))
                    WriteLine($"{opcodeMap[OpCode.PUSH]} {firstRhs}");
                else if (aliasMap.TryGetValue(firstRhs, out int aliasID))
                    WriteLine($"{opcodeMap[OpCode.PUSH]} #ref({aliasID})");
                else // assume value is a type literal
                    WriteLine($"{opcodeMap[OpCode.PUSH]} #type({firstRhs})");

                for (int i = 1; i < rhsParts.Count; i++)
                {
                    string part = rhsParts[i];
                    if (string.IsNullOrWhiteSpace(part))
                        continue;

                    if (part.Contains('(') && part.Contains(')'))
                        ParseMethodCall(id, part, isBody);
                    else
                    {
                        if (part.EndsWith(';'))
                            part = part[..^1];

                        WriteLine($"{opcodeMap[OpCode.ACCESS]} {part}");
                    }
                }
            }
            else
                throw new WinterForgeFormatException($"nothing to access on the right side...");

            return -1;
        }

        public static List<string> SplitPreserveParentheses(string input)
        {
            List<string> parts = new List<string>();
            int depth = 0;
            int start = 0;

            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] == '(') depth++;
                else if (input[i] == ')') depth--;
                else if (depth == 0 && i + 1 < input.Length && input[i] == '-' && input[i + 1] == '>')
                {
                    // split point
                    parts.Add(input[start..i]);
                    start = i + 2; // skip over ->
                    i++;
                }
            }

            // add the last part
            if (start < input.Length)
                parts.Add(input[start..]);

            return parts;
        }


        private void HandleAccessing(string? id, bool isBody, string? line = null, bool allowNoRHS = false)
        {
            string[] assignmentParts = (line ?? currentLine).Split('=', 2, StringSplitOptions.TrimEntries);
            string accessPart = assignmentParts[0];
            string? rhs = assignmentParts.Length > 1 ? assignmentParts[1] : null;

            // normalize trailing semicolon on RHS if present
            if (rhs != null && rhs.EndsWith(';'))
                rhs = rhs[..^1];

            // If there's no RHS, only allow that when the LHS contains a function call (parentheses).
            // Otherwise it's an error (missing RHS).
            if (rhs is null && !(accessPart.Contains('(') && accessPart.Contains(')')))
                if (!allowNoRHS)
                    throw new WinterForgeFormatException("Missing right-hand side for assignment and left-hand side is not a function call.");

            string val = "";
            if (rhs != null && rhs.Contains("->"))
                ParseRHSAccess(rhs, id, isBody);
            else if (rhs != null) // rhs may be null for allowed function-call-only LHS
            {
                val = ValidateValue(rhs, isBody, id);
                // if val is "#stack()" we used to plan special behavior — leave as-is for now
            }

            // Step 3: Process LHS
            var lhsParts = SplitPreserveParentheses(accessPart);

            if (lhsParts.Count == 0)
                return;

            string first = lhsParts[0];

            // Single-part LHS (e.g. "x = ...") — if RHS is missing but the single LHS is a function call,
            // we allow it (no assignment). Otherwise, generate a SET as before.
            if (lhsParts.Count == 1)
            {
                if (rhs is null)
                {
                    // LHS must be a function call to be allowed here. If it is, just return (call handling
                    // is expected to be produced elsewhere in your pipeline). If you want this method to
                    // emit call opcodes, add that here.
                    if (first.Contains('(') && first.Contains(')'))
                        return;

                    // not a function call and no RHS -> error (should have been caught earlier, but be safe)
                    throw new WinterForgeFormatException("Missing right-hand side for assignment.");
                }

                WriteLine($"{opcodeMap[OpCode.SET]} {first} #stack()");
                return;
            }

            // For multi-part LHS (a->b->c), push the root target first
            if (first == "this")
            {
                if (id is null)
                    throw new WinterForgeFormatException("'this' reference outside the bounds of an object");

                WriteLine($"{opcodeMap[OpCode.PUSH]} #ref({id})");
            }
            else if (first.StartsWith("#ref(") || variables.Contains(first))
                WriteLine($"{opcodeMap[OpCode.PUSH]} {first}");
            else if (aliasMap.TryGetValue(first, out int aliasID))
                WriteLine($"{opcodeMap[OpCode.PUSH]} #ref({aliasID})");
            else // assume value is a type literal
                WriteLine($"{opcodeMap[OpCode.PUSH]} #type({first})");

            for (int i = 1; i < lhsParts.Count; i++)
            {
                string part = lhsParts[i];
                if (string.IsNullOrWhiteSpace(part))
                    continue;

                bool isLast = i == lhsParts.Count - 1;

                // A function call on the left side is illegal *for assignments* (i.e. when rhs != null).
                // If rhs == null and this part is a function call, it's allowed (we simply don't throw).
                if (part.Contains('(') && part.Contains(')'))
                {
                    if (rhs != null)
                        throw new WinterForgeFormatException("Left hand side function is illegal when used as an lvalue for assignment.");
                    ParseMethodCall(id, part, isBody);
                    continue;
                }
                else
                {
                    if (rhs != null && isLast)
                    {
                        WriteLine($"{opcodeMap[OpCode.SETACCESS]} {part} {val}");
                    }
                    else
                        WriteLine($"{opcodeMap[OpCode.ACCESS]} {part}");
                }
            }
        }

        private void ParseMethodCall(string? id, string part, bool isBody)
        {
            var openParen = part.IndexOf('(');
            var closeParen = part.LastIndexOf(')');

            var methodName = part[..openParen].Trim();
            if (aliasMap.TryGetValue(methodName, out int key))
            {
                methodName = $"#ref({key})";
            }
            var argList = part.Substring(openParen + 1, closeParen - openParen - 1);
            var args = argList.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();

            for (int j = args.Count - 1; j >= 0; j--)
            {
                string arg = args[j];

                if (arg is "..")
                    continue; // assumed stack value exists from elsewhere

                if (arg.Contains("->"))
                    HandleAccessing(id, isBody, arg, true);
                else if (ContainsExpressionOutsideQuotes(arg))
                {
                    ParseExpression(arg, id, isBody);
                }
                else if (aliasMap.TryGetValue(arg, out int aliasID))
                {
                    WriteLine($"{opcodeMap[OpCode.PUSH]} #ref({aliasID})");
                }
                else
                    WriteLine($"{opcodeMap[OpCode.PUSH]} {arg}");
            }

            WriteLine($"{opcodeMap[OpCode.CALL]} {methodName} {args.Count}");
        }
        private int GetAutoID() => int.MaxValue - 1000 - autoAsIDs++;
        private CollectionParseResult ParseCollection(bool isBody)
        {
            int typeOpen = currentLine!.IndexOf('<');
            int blockOpen = currentLine.IndexOf('[');
            string start = currentLine[typeOpen..blockOpen];

            if (typeOpen == -1 || blockOpen == -1 || blockOpen < typeOpen)
                throw new Exception("Expected <TYPE> or <TYPE1, TYPE2> to indicate the type(s) of the collection before [");

            string types = start[1..^1];
            if (string.IsNullOrWhiteSpace(types))
                throw new Exception("Failed to parse types: " + currentLine);

            string[] typeParts = types.Split(',').Select(t => t.Trim()).ToArray();
            bool isDictionary = typeParts.Length == 2;

            if (!isDictionary && typeParts.Length != 1)
                throw new Exception("Invalid generic parameter count — expected one <T> for list or two <K, V> for dicts");

            if (isDictionary)
                WriteLine($"{opcodeMap[OpCode.LIST_START]} {typeParts[0]} {typeParts[1]}");
            else
                WriteLine($"{opcodeMap[OpCode.LIST_START]} {typeParts[0]}");

            bool insideFunction = false;
            StringBuilder currentElement = new();

            bool collectingDefinition = false;
            bool collectingString = false;
            int stringDepth = 0;
            char prefStringChar = '\0';
            int depth = 0;
            int listDepth = 1;
            char? currentChar;

            string cur = currentLine[(typeOpen + start.Length + 1)..];

            bool lastCharWasClose = false;
            var debugOutput = new StringBuilder();

            do
            {
                foreach (char c in cur)
                {
                    if (cur == "Name = \"Level 1\";")
                        ;
                    debugOutput.Append(c); // capture every character processed

                    int res = HandleChar(
                        ref insideFunction,
                        ref collectingString,
                        currentElement,
                        ref collectingDefinition,
                        ref depth,
                        ref isDictionary,
                        ref listDepth,
                        c,
                        ref prefStringChar,
                        isBody);

                    if (res == -1)
                        return isDictionary ? CollectionParseResult.Dictionary : CollectionParseResult.ListOrArray;
                }

                if (collectingDefinition)
                    currentElement.Append('\n');
                debugOutput.AppendLine();
            } while ((cur = ReadLine()) != null);

            // Dump debug output to file and open Notepad
            string tempFile = Path.Combine(Path.GetTempPath(), "WinterForgeDebugOutput.txt");

            debugOutput.AppendLine($"\n\n\nListDepth details:\nIncremented: {ldI}\nDecremeted: {ldD}");

            File.WriteAllText(tempFile, debugOutput.ToString());
            Process.Start(new ProcessStartInfo("notepad.exe", tempFile) { UseShellExecute = true });

            throw new WinterForgeFormatException("Expected ']' to close list.");
        }

        private void ParseAssignment(string line, string? id, bool isBody)
        {
            line = line.TrimEnd(';');
            int eq = line.IndexOf('=');
            string field = line[..eq].Trim();
            string value = ValidateValue(line[(eq + 1)..].Trim(), isBody, id);

            WriteLine($"{opcodeMap[OpCode.SET]} {field} {value}");
        }

        int ContainsSequenceOutsideBraces(StringBuilder sb, string sequence)
        {
            if (sequence.Length == 0) return 0;          // empty sequence is “found” at 0
            if (sb.Length < sequence.Length) return -1;  // obviously too short

            int braceDepth = 0;

            for (int i = 0; i <= sb.Length - sequence.Length; i++)
            {
                char current = sb[i];

                if (current == '{')
                {
                    braceDepth++;
                    continue;
                }

                if (current == '}')
                {
                    if (braceDepth > 0) braceDepth--;
                    continue;
                }

                if (braceDepth == 0)
                {
                    bool found = true;
                    for (int j = 0; j < sequence.Length; j++)
                    {
                        if (sb[i + j] != sequence[j])
                        {
                            found = false;
                            break;
                        }
                    }

                    if (found)
                        return i;
                }
            }

            return -1;
        }

        int ContainsSequenceOutsideQuotes(string text, string sequence)
        {
            if (sequence.Length == 0) return 0;                 // empty sequence is “found” at 0
            if (text.Length < sequence.Length) return -1;       // obviously too short

            bool insideQuotes = false;

            for (int i = 0; i <= text.Length - sequence.Length; i++)
            {
                char current = text[i];

                if (current == '"')
                {
                    bool escaped = i > 0 && text[i - 1] == '\\';
                    if (!escaped) insideQuotes = !insideQuotes;
                    continue;
                }

                if (!insideQuotes)
                {
                    bool found = true;
                    for (int j = 0; j < sequence.Length; j++)
                    {
                        if (text[i + j] != sequence[j])
                        {
                            found = false;
                            break;
                        }
                    }

                    if (found)
                        return i;
                }
            }

            return -1;
        }

        public static bool HasMoreThanOneOf(string input, char target)
        {
            int count = 0;

            foreach (char c in input)
            {
                if (c == target)
                {
                    if (++count > 1)
                        return true;
                }
            }

            return false;
        }

        public static bool ContainsExpressionOutsideQuotes(string input)
        {
            bool insideQuotes = false;

            int identifierCount = 0; // identifiers or typed literals
            int operatorCount = 0;   // math/boolean operators
            TokenType lastToken = TokenType.None;

            bool IsOperatorChar(char c) => "+-*/%><=!&|^".Contains(c);

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                // toggle quote state
                if (c == '"')
                {
                    bool escaped = i > 0 && input[i - 1] == '\\';
                    if (!escaped) insideQuotes = !insideQuotes;
                    continue;
                }

                if (insideQuotes) continue;

                // ignore whitespace, comma, semicolon
                if (char.IsWhiteSpace(c) || c == ',' || c == ';') continue;

                // typed literal |Type|Value
                if (c == '|' && i + 1 < input.Length)
                {
                    i++; // skip first '|'
                    while (i < input.Length && input[i] != '|') i++; // skip type
                    if (i < input.Length && input[i] == '|') i++; // skip closing '|'

                    // consume value part
                    while (i < input.Length && !char.IsWhiteSpace(input[i]) && !"+-*/%><=!&|^(),;".Contains(input[i]))
                        i++;

                    identifierCount++;
                    lastToken = TokenType.Identifier;
                    i--;
                    continue;
                }

                // numbers (including signed and comma/decimal numbers)
                if (char.IsDigit(c) || ((c == '-' || c == '+') && i + 1 < input.Length && char.IsDigit(input[i + 1])))
                {
                    // consume full number literal
                    if (c == '-' || c == '+') i++; // skip sign
                    while (i < input.Length && (char.IsDigit(input[i]) || input[i] == '.' || input[i] == ',')) i++;
                    lastToken = TokenType.Identifier;
                    identifierCount++;
                    i--;
                    continue;
                }

                // identifiers (variables, function names, etc.)
                if (char.IsLetter(c) || c == '_')
                {
                    while (i < input.Length && (char.IsLetterOrDigit(input[i]) || input[i] == '_' || input[i] == '(' || input[i] == ')')) i++;
                    lastToken = TokenType.Identifier;
                    identifierCount++;
                    i--;
                    continue;
                }

                // operators
                if (IsOperatorChar(c))
                {
                    // Try to read the longest operator token starting at i
                    int start = i;
                    int end = i + 1;

                    while (end < input.Length && IsOperatorChar(input[end]))
                        end++;

                    string opToken = input[start..end];

                    // Handle single '=' as assignment, not operator
                    if (opToken == "=")
                    {
                        lastToken = TokenType.None;
                        i = end; // skip this single character
                        continue;
                    }

                    if (opToken is "->")
                    {
                        lastToken = TokenType.Identifier;
                        i = end - 1;
                        continue;
                    }

                    // If there's at least one identifier/typed literal before, count this as operator
                    if (identifierCount > 0)
                        operatorCount++;

                    lastToken = TokenType.Operator;
                    i = end - 1; // Skip past entire operator token
                    continue;
                }
            }

            // must have at least 2 operands and 1 operator, and end with an operand
            return identifierCount >= 2 && operatorCount >= 1 && lastToken == TokenType.Identifier;
        }

        private string ValidateValue(string value, bool isBody, string? id = null)
        {
            if (value.StartsWith('\"') && value.StartsWith('\"'))
            {
                string fullString = ReadString(value);

                if (!fullString.Contains('\n'))
                    return fullString;
                else
                {
                    writer.WriteLine(opcodeMap[OpCode.START_STR]);
                    foreach (string line in fullString.Split('\n'))
                        writer.WriteLine($"{opcodeMap[OpCode.STR]} \"{line}\"");
                    writer.WriteLine(opcodeMap[OpCode.END_STR]);

                    return "#stack()";
                }
            }
            else if (HasValidGenericFollowedByBracket(value))
            {
                TryParseCollection(value, out string name, isBody);
                return name;
            }
            else if (ContainsExpressionOutsideQuotes(value))
            {
                ParseExpression(value, id, isBody);
                return "#stack()";
            }
            else if (ContainsSequenceOutsideQuotes(value, "->") != -1)
            {
                HandleAccessing(null, isBody, value);
                return "#stack()";
            }
            else if (value.StartsWith("#type"))
                return value;
            else if (value.StartsWith("#ref"))
                return value;
            else if (value.StartsWith("#stack"))
                return value;
            else if (value.Contains('.') && !IsValidNumericString(value) && !value.Contains('<'))
            {
                string[] parts = value.Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                string enumType;
                string enumValue;
                if (parts.Length > 2)
                {
                    enumType = parts.Take(parts.Length - 1).Aggregate((a, b) => a + "." + b);
                    enumValue = parts.Last();
                }
                else if (parts.Length == 2)
                {
                    enumType = parts[0];
                    enumValue = parts[1];
                }
                else
                    throw new WinterForgeFormatException(value, "Invalid enum format. Expected 'EnumType.EnumValue' or 'Namespace.EnumType.EnumValue'");

                Type? e = TypeWorker.FindType(enumType);
                if (!e.IsEnum)
                    throw new WinterForgeFormatException(value, $"Type '{enumType}' is not an enum type.");

                string[] values = enumValue.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                Enum result = null!;
                for (int i = 0; i < values.Length; i++)
                {
                    string v = values[i];
                    if (!Enum.IsDefined(e, v))
                        throw new WinterForgeFormatException(value, $"Enum '{enumType}' does not contain value '{enumValue}'.");

                    object parsedEnumValue = Enum.Parse(e, v);
                    if (i == 0)
                        result = (Enum)parsedEnumValue;
                    else
                        result = (Enum)Enum.ToObject(e, Convert.ToInt32(result) | Convert.ToInt32(parsedEnumValue));
                }

                return Convert.ChangeType(result, Enum.GetUnderlyingType(e)).ToString()!;
            }
            else if (value is "true")
            {
                WriteLine($"{opcodeMap[OpCode.PUSH]} true");
                return "#stack()";
            }
            else if (value is "false")
            {
                WriteLine($"{opcodeMap[OpCode.PUSH]} false");
                return "#stack()";
            }
            else if (IsMethodCall(value))
            {
                ParseMethodCall(id, value, isBody);
                return "#stack()";
            }
            return value;
        }
        private bool IsMethodCall(string line)
        {
            return ContainsSequenceOutsideQuotes(line, "(") != -1 && line.EndsWith(')');
        }
        private void ParseExpression(string value, string? id, bool isBody)
        {
            if (value.EndsWith(';'))
                value = value[..^1];
            var tokens = ExpressionTokenizer.Tokenize(value);

            foreach (var token in tokens)
            {
                switch (token.Type)
                {
                    case TokenType.Number:
                    case TokenType.String:
                    case TokenType.Identifier:
                        if (ContainsSequenceOutsideQuotes(token.Text, "->") != -1)
                        {
                            ParseRHSAccess(token.Text, id, isBody);
                            break;
                        }
                        else if (token.Text.EndsWith(')') && token.Text.Contains('('))
                        {
                            ParseMethodCall(id, token.Text, isBody);
                        }
                        else
                            WriteLine($"{opcodeMap[OpCode.PUSH]} {token.Text}");
                        break;
                    case TokenType.Operator:
                        OpCode operatorCode = token.Text switch
                        {
                            "+" => OpCode.ADD,
                            "-" => OpCode.SUB,
                            "*" => OpCode.MUL,
                            "/" => OpCode.DIV,
                            "%" => OpCode.MOD,
                            "^" => OpCode.POW,

                            "==" => OpCode.EQ,
                            "!=" => OpCode.NEQ,
                            ">" => OpCode.GT,
                            "<" => OpCode.LT,
                            ">=" => OpCode.GTE,
                            "<=" => OpCode.LTE,

                            "&&" => OpCode.AND,
                            "||" => OpCode.OR,
                            "!" => OpCode.NOT,
                            "^|" => OpCode.XOR,

                            _ => throw new InvalidOperationException($"Unknown operator: {token.Text}")
                        };

                        WriteLine(opcodeMap[operatorCode].ToString());
                        break;
                    case TokenType.LParen:
                        break;
                    case TokenType.RParen:
                        break;
                    default:
                        break;
                }
            }
        }
        public static bool IsValidNumericString(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            int dotCount = 0;
            int commaCount = 0;

            foreach (char ch in input)
            {
                if (ch == '.') dotCount++;
                if (ch == ',') commaCount++;
            }

            if (dotCount > 1 || commaCount > 1) return false;      // too many separators
            if (dotCount > 0 && commaCount > 0) return false;      // mixed separators

            string normalized = commaCount > 0
                ? input.Replace(',', '.')                          // unify on '.'
                : input;

            double parsedNumber;
            return double.TryParse(
                normalized,
                NumberStyles.Float | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out parsedNumber);
        }
        private string ReadString(string start)
        {
            if (start is "\"\"")
                return start;
            StringBuilder content = new("");
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
                        _ => throw new WinterForgeFormatException("Invalid escape character \\" + c)
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
                        return '\"' + content.ToString() + '\"';
                    else if (!isMultiline)
                    {
                        return '\"' + content.ToString() + '\"';
                    }
                    content.Append('"');
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
                string? nextLine = ReadLine(allowEmptyLines: true);
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
                            return '\"' + content.ToString() + '\"';
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
        internal string? ReadLine(bool allowEmptyLines = false)
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

        private string? PeekNonEmptyLine(bool allowEmptyLines = false)
        {
            string? line = null;

            if (lineBuffers.Count > 0)
            {
                var topBuffer = lineBuffers.Peek();
                if ((topBuffer?.Count ?? 0) > 0)
                {
                    line = topBuffer.Peek();
                }
                else
                {
                    return null;
                }
            }
            else
            {
                if (reader.EndOfStream)
                    return null;

                // Read once, but push back into buffer so it can be read again later
                line = reader.ReadLine();
                if (line != null)
                {
                    var tempStack = new OverridableStack<string>();
                    tempStack.PushStart(line);
                    lineBuffers.Push(tempStack);
                }
            }

            if (!allowEmptyLines && string.IsNullOrWhiteSpace(line))
                return null;

            return line;
        }

        private void ReadNextLineExpecting(string expected)
        {
            currentLine = ReadLine();
            if (currentLine == null || currentLine.Trim() != expected)
                throw new Exception($"Expected '{expected}' but got: {currentLine}");
        }
        private void WriteLine(string line)
        {
            if (line.Contains('\n'))
            {
                int lastIndex = line.Length - 1;

                // Walk backwards to find where the trailing newlines start
                while (lastIndex >= 0 && line[lastIndex] == '\n')
                {
                    lastIndex--;
                }

                string sanitized = line[..(lastIndex + 1)].Replace("\n", "");
                line = sanitized;
            }
            writer.WriteLine(line);
        }
        int HandleChar(ref bool insideFunction, ref bool collectingString, StringBuilder currentElement, ref bool collectingDefinition, ref int depth, ref bool isDictionary, ref int listDepth, char? currentChar, ref char prefStringChar, bool isBody)
        {
            char character = currentChar.Value;

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

            if (character == '<' && !collectingDefinition)
            {
                collectingDefinition = true;
                currentElement.Append(character);
                return 1;
            }

            if (character == '}')
            {
                depth--;

                currentElement.Append(character);

                if (listDepth is 0 or 1 && depth <= 0)
                {
                    string s = currentElement.ToString();
                    int dvsp = -1;
                    if (isDictionary && (dvsp = ContainsSequenceOutsideBraces(currentElement, "=>")) == -1)
                        return 1; // skip emiting the element when not complete yet

                    collectingDefinition = false;
                    EmitElement(currentElement, isDictionary, dvsp, isBody);
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

            if (!insideFunction && !collectingString && character == ',')
            {
                if (prefStringChar == '}')
                    collectingDefinition = false;
                int d = depth;
                if (listDepth is 1 or 0 && !collectingDefinition)
                {
                    EmitElement(currentElement, isDictionary, -1, isBody);
                }
                else
                    currentElement.Append(character);

                return 1;
            }

            if (!insideFunction && character == '[')
            {
                ldI++;
                listDepth++;
                collectingDefinition = true;

                if (listDepth is 5)
                    ;
            }

            if (!insideFunction && character == ']')
            {
                // Now actually close one list level if possible.
                if (listDepth > 0)
                {
                    listDepth--;
                    ldD++;
                    if (collectingDefinition)
                        currentElement.Append(character);
                }
                else
                {
                    // Defensive: unexpected close bracket (log and recover)
                    Console.WriteLine("WARNING: unexpected ']' while listDepth == 0");
                }

                // If there's a pending element and we are not currently collecting an in-object definition,
                // flush it before closing the list.
                if (!collectingDefinition && listDepth > 0)
                {
                    EmitElement(currentElement, isDictionary, -1, isBody);
                }

                // If after decrementing we reached the root list, finish parsing collection.
                if (listDepth == 0)
                {
                    // Emit any trailing content (safe-guard)
                    EmitElement(currentElement, isDictionary, -1, isBody);
                    currentElement.Clear();
                    collectingDefinition = false;
                    WriteLine(opcodeMap[OpCode.LIST_END].ToString());
                    return -1;
                }

                return 1;
            }

            //if (!collectingDefinition || listDepth is 0 or 1 && char.IsWhiteSpace(character))
            //    return 1;
            currentElement.Append(character);
            return 0;
        }
        void EmitElement(StringBuilder elementSB, bool isDictionary, int dictValueSplitterIndex, bool isBody)
        {
            if (elementSB.Length == 0)
                return;

            string currentElement = elementSB.ToString();
            elementSB.Clear();
            if (string.IsNullOrWhiteSpace(currentElement))
                return;
            if (currentElement.Contains(':'))
            {
                if (isDictionary)
                {
                    int dictKVsplit = dictValueSplitterIndex == -1 ? currentElement.IndexOf("=>") : dictValueSplitterIndex;
                    if (dictKVsplit == -1)
                        throw new WinterForgeFormatException(currentElement, "Dictionary key-value not properly written. Expected 'key => value'");

                    string rawKey = currentElement[..dictKVsplit].Trim();
                    string rawValue = currentElement[(dictKVsplit + 2)..].Trim();

                    string keyResult;
                    string valueResult;

                    // KEY PARSING
                    var lines = rawKey.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length >= 1 && lines[0].Contains(':'))
                    {
                        lineBuffers.Push(new OverridableStack<string>());

                        for (int i = 0; i < lines.Length; i++)
                            lineBuffers.Peek().PushEnd(lines[i].Trim() + '\n');

                        currentLine = ReadLine();
                        int colonIndex = currentLine.IndexOf(':');
                        int braceIndex = currentLine.IndexOf('{');

                        string id = braceIndex == -1
                            ? currentLine[colonIndex..].Trim()
                            : currentLine.Substring(colonIndex + 1, braceIndex - colonIndex - 1).Trim();

                        ParseObjectOrAssignment(isBody);
                        keyResult = $"#ref({id})";
                    }
                    else
                    {
                        keyResult = ValidateValue(rawKey, isBody, null);
                    }

                    // VALUE PARSING
                    lines = rawValue.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length >= 1 && lines[0].Contains(':'))
                    {
                        // Push full remaining lines
                        lineBuffers.Push(new OverridableStack<string>());
                        ;
                        for (int i = 0; i < lines.Length; i++)
                            lineBuffers.Peek().PushEnd(lines[i].Trim() + '\n');

                        currentLine = ReadLine();
                        int colonIndex = currentLine.IndexOf(':');
                        int braceIndex = currentLine.IndexOf('{');

                        string id = braceIndex == -1
                            ? currentLine[colonIndex..].Trim()
                            : currentLine.Substring(colonIndex + 1, braceIndex - colonIndex - 1).Trim();

                        ParseObjectOrAssignment(isBody);
                        valueResult = $"#ref({id})";
                    }
                    else
                    {
                        valueResult = ValidateValue(rawValue, isBody);
                    }

                    WriteLine($"{opcodeMap[OpCode.ELEMENT]} {keyResult} {valueResult}");
                }
                else
                {
                    // existing object parsing logic
                    lineBuffers.Push(new OverridableStack<string>());
                    var lines = currentElement.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                        lineBuffers.Peek().PushEnd(line.Trim() + '\n');

                    currentLine = ReadLine();
                    int colonIndex = currentLine.IndexOf(':');
                    int braceIndex = currentLine.IndexOf('{');

                    string id = braceIndex == -1
                        ? currentLine[colonIndex..].Trim()
                        : currentLine.Substring(colonIndex + 1, braceIndex - colonIndex - 1).Trim();

                    ParseObjectOrAssignment(isBody);
                    WriteLine($"{opcodeMap[OpCode.ELEMENT]} #ref({id})");
                }
            }
            else if (isDictionary && !currentElement.Contains("=<"))
            {
                string[] parts = currentElement.Split("=>", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length == 0)
                    throw new WinterForgeFormatException(currentElement, "No dictionary key-value given");
                if (parts.Length == 1)
                    throw new WinterForgeFormatException(currentElement, "Only a key was given for the dictionary");

                parts[0] = ValidateValue(parts[0], isBody);
                parts[1] = ValidateValue(parts[1], isBody);

                WriteLine($"{opcodeMap[OpCode.ELEMENT]} {parts[0]} {parts[1]}");
            }
            else
            {
                currentElement = ValidateValue(currentElement, isBody);
                WriteLine($"{opcodeMap[OpCode.ELEMENT]} " + currentElement);
            }
        }

        internal void EnqueueLines(List<string> lines)
        {
            lineBuffers.Push(new(lines));
        }
    }
}
