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
        private string? currentLine;
        private int depth = 0;
        private Dictionary<string, int> aliasMap = [];
        private readonly Stack<OverridableStack<string>> lineBuffers = new();
        List<int> foundIds = [];
        private int autoAsIDs;
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
            writer = new StreamWriter(output, Encoding.UTF8);

            string version = typeof(WinterForge)
                .Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                .InformationalVersion;

            WriteLine($"// Parsed by WinterForge {version.Split('+')[0]}");
            WriteLine("");
            WriteLine("");

            while ((currentLine = ReadNonEmptyLine()) != null)
                ParseObjectOrAssignment();

            if (output is NetworkStream)
                writer.WriteLine("WF_ENDOFDATA");
            writer.Flush();
        }

        private void ParseObjectOrAssignment()
        {
            string line = currentLine!.Trim();

            if (line.Trim().StartsWith("//"))
                return;

            if (TryParseFirstParts())
                return;
            // Constructor Definition: Type(arguments) : ID {
            if (line.Contains('(') && line.Contains(')') && ContainsSequenceOutsideQuotes(line, ":") != -1 && line.Contains('{'))
            {
                int openParenIndex = line.IndexOf('(');
                int closeParenIndex = line.IndexOf(')');
                int colonIndex = line.IndexOf(':');
                int braceIndex = line.IndexOf('{');

                string type = line[..openParenIndex].Trim();
                string arguments = line.Substring(openParenIndex + 1, closeParenIndex - openParenIndex - 1).Trim();
                string id = line.Substring(colonIndex + 1, braceIndex - colonIndex - 1).Trim();

                if (id is "nextid")
                    id = GetAutoID().ToString();
                foundIds.Add(int.Parse(id));

                var args = arguments.Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                foreach (string arg in args)
                    WriteLine($"{opcodeMap["PUSH"]} " + arg);
                WriteLine($"{opcodeMap["DEFINE"]} {type} {id} {args.Length}");
                depth++;
                ParseBlock(id);
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
                string id = line.Substring(colonIndex + 1, line.Length - colonIndex - 2).Trim();

                if (id is "nextid")
                    id = GetAutoID().ToString();
                foundIds.Add(int.Parse(id));

                var args = arguments.Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                foreach (string arg in args)
                    WriteLine($"{opcodeMap["PUSH"]} " + arg);
                WriteLine($"{opcodeMap["DEFINE"]} {type} {id} {args.Length}");
                WriteLine($"{opcodeMap["END"]} {id}");
            }
            // Definition: Type : ID {
            else if (ContainsSequenceOutsideQuotes(line, ":") != -1 && line.Contains('{'))
            {
                int colonIndex = line.IndexOf(':');
                int braceIndex = line.IndexOf('{');

                string type = line[..colonIndex].Trim();
                if (type.Contains("Anonymous"))
                    type = type.Replace(' ', '-');

                string id = line.Substring(colonIndex + 1, braceIndex - colonIndex - 1).Trim();

                if (id is "nextid")
                    id = GetAutoID().ToString();
                foundIds.Add(int.Parse(id));

                WriteLine($"{opcodeMap["DEFINE"]} {type} {id} 0");
                depth++;
                ParseBlock(id);
            }
            // Definition: Type : ID;
            else if (ContainsSequenceOutsideQuotes(line, ":") != -1 && line.EndsWith(';'))
            {
                string type;
                string id;

                var parts = line[..^1].Split(':');
                type = parts[0].Trim();
                if (type.Contains("Anonymous"))
                    type = type.Replace(' ', '-');
                id = parts[1].Trim();

                if (id is "nextid")
                    id = GetAutoID().ToString();
                foundIds.Add(int.Parse(id));

                WriteLine($"{opcodeMap["DEFINE"]} {type} {id} 0");
                WriteLine($"{opcodeMap["END"]} {id}");
            }
            // Definition: Type : ID
            else if (ContainsSequenceOutsideQuotes(line, ":") != -1)
            {
                string type;
                string id;

                var parts = line.Split(':');
                type = parts[0].Trim();
                if (type.Contains("Anonymous"))
                    type = type.Replace(' ', '-');
                id = parts[1].Trim();

                if (id is "nextid")
                    id = GetAutoID().ToString();

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
                if (string.IsNullOrWhiteSpace(ID) || !ID.All(char.IsDigit) && ID != "_stack()" && ID != "null")
                    if (aliasMap.TryGetValue(ID, out int aliasID))
                        ID = aliasID.ToString();
                    else
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
                    id = GetAutoID().ToString();

                WriteLine($"{opcodeMap["AS"]} {id}");
                foundIds.Add(int.Parse(id));
            }
            else if (HasValidGenericFollowedByBracket(line))
            {
                ParseCollection();
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
            else
                throw new Exception($"Unexpected top-level line: {line}");
        }

        private bool TryParseFirstParts()
        {
            if (currentLine.StartsWith("#import", StringComparison.OrdinalIgnoreCase))
            {
                string line = currentLine[7..].Trim();
                string importedFileName = ReadString(line)[1..^1];
                line = line[(importedFileName.Length + 2)..].Trim();
                if (!line.StartsWith("as"))
                    throw new WinterForgeFormatException("Missing 'as' keyword in #import statement");

                line = line[2..].Trim();

                int aliasEndIndex = line.IndexOf(' ');
                if (aliasEndIndex is -1)
                    aliasEndIndex = line.IndexOf(';');
                if (aliasEndIndex is -1)
                    aliasEndIndex = line.Length;

                string alias = line[..aliasEndIndex].Trim();
                int id = GetAutoID();
                aliasMap[alias] = id;
                line = line[alias.Length..].Trim();

                if (line.StartsWith("(compiles into "))
                {
                    if (!line.EndsWith(')'))
                        throw new WinterForgeFormatException("Import compile statement not closed with )");
                    // compile imported file
                    line = line[15..^1];

                    WinterForge.ConvertFromFileToFile(importedFileName, line);
                    importedFileName = line;
                }

                WriteLine($"{opcodeMap["IMPORT"]} \"{importedFileName}\" {id}");
                return true;
            }
            return false;
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

        private void ParseBlock(string id)
        {
            while ((currentLine = ReadNonEmptyLine()) != null)
            {
                string line = currentLine.Trim();

                if (line.Trim().StartsWith("//"))
                    continue;

                if (line == "}")
                {
                    depth--;

                    WriteLine($"{opcodeMap["END"]} {id}");
                    return;
                }
                if (ContainsExpressionOutsideQuotes(line) &&
                    line.Contains(" = ") && line.EndsWith(';'))
                    ParseAssignment(line, id);
                else if (line.Contains("->"))
                    HandleAccessing(id);
                else if (line.IndexOf(':') is int colinx && line.IndexOf('=') is int eqinx
                    && colinx is not -1 && eqinx is not -1
                    && colinx < eqinx)
                    ParseAnonymousAssignment(line);
                else if (line.Contains('=') && line.EndsWith(';'))
                    ParseAssignment(line, id);
                else if (line.Contains('=') && line.Contains('\"'))
                    ParseAssignment(line, id);
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
                else if (TryParseCollection(line, out _) is var colres && colres is not CollectionParseResult.Failed or CollectionParseResult.NotACollection)
                {
                    continue;
                }
                else
                {
                    throw new Exception($"unexpected block content: {line}");
                }
            }
        }

        private CollectionParseResult TryParseCollection(string line, out string name)
        {
            if (!line.Contains('['))
            {
                name = line;
                return CollectionParseResult.NotACollection;
            }
            name = "_stack()";

            currentLine = line;
            CollectionParseResult result = ParseCollection();

            int colonIndex = line.IndexOf(':');
            if (colonIndex is -1)
                colonIndex = line.IndexOf('[');
            int equalsIndex = line.IndexOf('=');
            if (equalsIndex != -1 && colonIndex > equalsIndex)
            {
                name = line[..equalsIndex].Trim();
                if (!string.IsNullOrEmpty(name))
                    WriteLine($"{opcodeMap["SET"]} {name} _stack()");
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


        private void ParseAnonymousAssignment(string line)
        {
            // Example: "type:name = value"
            int colonIndex = line.IndexOf('=');
            if (colonIndex == -1)
                throw new Exception("Invalid anonymous assignment format, missing ':'");
            string typeAndName = line[..colonIndex].Trim();
            string value = line[(colonIndex + 1)..].Trim();

            TryParseCollection(value, out value);

            if (value.EndsWith(';'))
                value = value[..^1].Trim();
            if (string.IsNullOrWhiteSpace(typeAndName) || string.IsNullOrWhiteSpace(value))
                throw new Exception("Invalid anonymous assignment format, type or name is empty");
            string[] typeAndNameParts = typeAndName.Split(':', 2, StringSplitOptions.TrimEntries);
            if (typeAndNameParts.Length != 2)
                throw new Exception("Invalid anonymous assignment format, expected 'type:name'");
            string type = typeAndNameParts[0];
            string name = typeAndNameParts[1];

            WriteLine($"{opcodeMap["ANONYMOUS_SET"]} {type} {name} {value}");
        }

        private int ParseRHSAccess(string rhs, string? id)
        {
            var rhsParts = rhs.Split("->", StringSplitOptions.RemoveEmptyEntries);

            if (rhsParts.Length > 0)
            {
                string firstRhs = rhsParts[0];

                if (firstRhs == "this")
                {
                    if (id is null)
                        throw new WinterForgeFormatException("'this' reference outside the bounds of an object");

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
                        ParseMethodCall(id, part);
                    else
                    {
                        if (part.EndsWith(';'))
                            part = part[..^1];

                        WriteLine($"{opcodeMap["ACCESS"]} {part}");
                    }
                }
            }
            else
                throw new WinterForgeFormatException($"nothing to access on the right side...");

            //if (rhs.Contains("->"))
            //{
            //    int asID = GetAutoID();
            //    WriteLine($"{opcodeMap["AS"]} {asID}");
            //    return asID;
            //}
            return -1;
        }

        private void HandleAccessing(string? id)
        {
            string[] assignmentParts = currentLine.Split('=', 2, StringSplitOptions.TrimEntries);
            string accessPart = assignmentParts[0];
            string? rhs = assignmentParts.Length > 1 ? assignmentParts[1] : null;

            //int asID = -1;
            string val = "";
            if (rhs != null && rhs.Contains("->"))
                ParseRHSAccess(rhs, id);
            else
            {
                if (rhs.EndsWith(';'))
                    rhs = rhs[..^1];
                val = ValidateValue(rhs);
                if (val is "_stack()")
                {
                    //if (ContainsExpressionOutsideQuotes(rhs))
                    //{
                    //asID = GetAutoID();
                    //WriteLine($"{opcodeMap["AS"]} {asID}");
                    //}

                    //val = $"_ref({asID})";
                }
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
                    throw new WinterForgeFormatException("'this' reference outside the bounds of an object");

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
                    throw new WinterForgeFormatException("Left hand side function is illegal.");
                else
                {
                    if (rhs != null && isLast)
                    {
                        WriteLine($"{opcodeMap["SETACCESS"]} {part} {val}");
                    }
                    else
                        WriteLine($"{opcodeMap["ACCESS"]} {part}");
                }
            }


        }

        private void ParseMethodCall(string? id, string part)
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

        private int GetAutoID() => int.MaxValue - 1000 - autoAsIDs++;

        private CollectionParseResult ParseCollection()
        {
            int typeOpen = currentLine!.IndexOf("<");
            int blockOpen = currentLine.IndexOf("[");
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
                WriteLine($"{opcodeMap["LIST_START"]} {typeParts[0]} {typeParts[1]}");
            else
                WriteLine($"{opcodeMap["LIST_START"]} {typeParts[0]}");

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
            do
            {
                foreach (char c in cur)
                {
                    int res = handleChar(ref insideFunction, currentElement, ref collectingDefinition, ref depth, ref listDepth, c, ref prefStringChar);
                    if (res == -1)
                        return isDictionary ? CollectionParseResult.Dictionary : CollectionParseResult.ListOrArray;
                }
                if (collectingDefinition)
                    currentElement.Append('\n');
            } while ((cur = ReadNonEmptyLine()) != null);

            writer.Flush();
            throw new Exception("Expected ']' to close list.");

            void emitElement(int dictValueSplitterIndex = -1)
            {
                if (currentElement.Length <= 0)
                    return;

                string currentElementString = currentElement.ToString();
                if (string.IsNullOrWhiteSpace(currentElementString))
                    return;
                if (currentElementString.Contains(':'))
                {
                    if (isDictionary)
                    {
                        int dictKVsplit = dictValueSplitterIndex == -1 ? currentElementString.IndexOf("=>") : dictValueSplitterIndex;
                        if (dictKVsplit == -1)
                            throw new WinterForgeFormatException(currentElementString, "Dictionary key-value not properly written. Expected 'key => value'");

                        string rawKey = currentElementString[..dictKVsplit].Trim();
                        string rawValue = currentElementString[(dictKVsplit + 2)..].Trim();

                        string keyResult;
                        string valueResult;

                        // KEY PARSING
                        var lines = rawKey.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                        if (lines.Length >= 1 && lines[0].Contains(':'))
                        {
                            lineBuffers.Push(new OverridableStack<string>());

                            for (int i = 0; i < lines.Length; i++)
                                lineBuffers.Peek().PushEnd(lines[i].Trim() + '\n');

                            currentLine = ReadNonEmptyLine();
                            int colonIndex = currentLine.IndexOf(':');
                            int braceIndex = currentLine.IndexOf('{');

                            string id = braceIndex == -1
                                ? currentLine[colonIndex..].Trim()
                                : currentLine.Substring(colonIndex + 1, braceIndex - colonIndex - 1).Trim();

                            ParseObjectOrAssignment();
                            keyResult = $"_ref({id})";
                        }
                        else
                        {
                            keyResult = ValidateValue(rawKey);
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

                            currentLine = ReadNonEmptyLine();
                            int colonIndex = currentLine.IndexOf(':');
                            int braceIndex = currentLine.IndexOf('{');

                            string id = braceIndex == -1
                                ? currentLine[colonIndex..].Trim()
                                : currentLine.Substring(colonIndex + 1, braceIndex - colonIndex - 1).Trim();

                            ParseObjectOrAssignment();
                            valueResult = $"_ref({id})";
                        }
                        else
                        {
                            valueResult = ValidateValue(rawValue);
                        }

                        WriteLine($"{opcodeMap["ELEMENT"]} {keyResult} {valueResult}");
                    }
                    else
                    {
                        // existing object parsing logic
                        lineBuffers.Push(new OverridableStack<string>());
                        var lines = currentElementString.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in lines)
                            lineBuffers.Peek().PushEnd(line.Trim() + '\n');

                        currentLine = ReadNonEmptyLine();
                        int colonIndex = currentLine.IndexOf(':');
                        int braceIndex = currentLine.IndexOf('{');

                        string id = braceIndex == -1
                            ? currentLine[colonIndex..].Trim()
                            : currentLine.Substring(colonIndex + 1, braceIndex - colonIndex - 1).Trim();

                        ParseObjectOrAssignment();
                        WriteLine($"{opcodeMap["ELEMENT"]} _ref({id})");
                    }
                }
                else if (isDictionary && !currentElementString.Contains("=<"))
                {
                    string[] parts = currentElementString.Split("=>", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (parts.Length == 0)
                        throw new WinterForgeFormatException(currentElementString, "No dictionary key-value given");
                    if (parts.Length == 1)
                        throw new WinterForgeFormatException(currentElementString, "Only a key was given for the dictionary");

                    parts[0] = ValidateValue(parts[0]);
                    parts[1] = ValidateValue(parts[1]);

                    WriteLine($"{opcodeMap["ELEMENT"]} {parts[0]} {parts[1]}");
                }
                else
                {
                    currentElementString = ValidateValue(currentElementString);
                    WriteLine($"{opcodeMap["ELEMENT"]} " + currentElementString);
                }
                currentElement.Clear();
            }

            int handleChar(ref bool insideFunction, StringBuilder currentElement, ref bool collectingDefinition, ref int depth, ref int listDepth, char? currentChar, ref char prefStringChar)
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
                        emitElement(dvsp);
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
                    if (listDepth is 1 && !collectingDefinition)
                        emitElement();
                    else
                        currentElement.Append(character);

                    return 1;
                }

                if (!insideFunction && character == '[')
                {
                    listDepth++;
                    collectingDefinition = true;
                }

                if (!insideFunction && character == ']')
                {
                    if (listDepth is not 0)
                        listDepth--;
                    else
                        ;

                    if (listDepth is not <= 0 || collectingDefinition)
                    {
                        if (listDepth is not 0)
                            currentElement.Append("\n]\n");

                        if (collectingDefinition && listDepth is 0)
                            collectingDefinition = false;
                    }
                    if (listDepth is 0)
                    {
                        emitElement();
                        WriteLine(opcodeMap["LIST_END"].ToString());
                    }

                    if (listDepth == 0)
                        return -1;
                    else return 1;
                }

                //if (!collectingDefinition || listDepth is 0 or 1 && char.IsWhiteSpace(character))
                //    return 1;
                currentElement.Append(character);
                return 0;
            }

        }

        private void ParseAssignment(string line, string? id)
        {
            line = line.TrimEnd(';');
            int eq = line.IndexOf('=');
            string field = line[..eq].Trim();
            string value = ValidateValue(line[(eq + 1)..].Trim(), id);

            WriteLine($"{opcodeMap["SET"]} {field} {value}");
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
            bool insideGeneric = false;

            // Operators considered for math/boolean expressions
            string mathBoolOperators = "+-*/%==!=><>=<=&&||!^";
            TokenType lastToken = TokenType.None;

            // Simple helper to check if char is operator char used in math/boolean operators
            bool IsOperatorChar(char c) => "+-*/%><=!&|^".Contains(c);

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                if (c == '"')
                {
                    bool escaped = i > 0 && input[i - 1] == '\\';
                    if (!escaped)
                        insideQuotes = !insideQuotes;
                    continue;
                }

                if (insideQuotes)
                    continue;

                if (c == '<' && lastToken == TokenType.Identifier)
                {
                    // Entering generic syntax - skip until matching '>'
                    insideGeneric = true;
                    continue;
                }

                if (c == '>' && insideGeneric)
                {
                    insideGeneric = false;
                    continue;
                }

                if (insideGeneric)
                    continue;

                if (char.IsWhiteSpace(c) || c == ',' || c == ';')
                {
                    // Reset last token on separators, but only if expression isn't found yet
                    continue;
                }

                if (char.IsLetterOrDigit(c) || c == '_')
                {
                    lastToken = TokenType.Identifier;
                    continue;
                }

                if (IsOperatorChar(c))
                {
                    // If operator is '=' alone and previous token is None or operator, treat as assignment or invalid - ignore
                    if (c == '=')
                    {
                        bool isDoubleEqual = i + 1 < input.Length && input[i + 1] == '=';
                        if (!isDoubleEqual)
                        {
                            // Single = likely assignment, skip
                            lastToken = TokenType.None;
                            continue;
                        }
                    }

                    // Check if last token was operand and operator is valid math/boolean operator
                    if (lastToken == TokenType.Identifier)
                    {
                        lastToken = TokenType.Operator;
                        continue;
                    }

                    // Operator without operand before = no expression yet
                    lastToken = TokenType.None;
                    continue;
                }

                // Anything else resets state
                lastToken = TokenType.None;
            }

            // Valid expression requires operand-operator-operand sequence
            // So if last token was operator, invalid (e.g. "5 +")
            return lastToken == TokenType.Operator;
        }


        private string ValidateValue(string value, string? id = null)
        {
            if (value.StartsWith('\"') && value.StartsWith('\"'))
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

            if (ContainsExpressionOutsideQuotes(value))
            {
                ParseExpression(value, id);
                return "_stack()";
            }

            if (value.StartsWith("_type"))
                return value;

            if (HasValidGenericFollowedByBracket(value))
            {
                TryParseCollection(value, out string name);
                return name;
            }
            if (value.Contains('.') && !IsValidNumericString(value) && !value.Contains('<'))
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
            return value;
        }

        private void ParseExpression(string value, string? id)
        {
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
                            ParseRHSAccess(token.Text, id);
                            break;
                        }
                        WriteLine($"{opcodeMap["PUSH"]} {token.Text}");
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

                        WriteLine(opcodeMap[operatorCode.ToString()].ToString());
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
    }

}
