using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using WinterRose.WinterForgeSerializing.Containers;
using WinterRose.WinterForgeSerializing.Instructions;

namespace WinterRose.WinterForgeSerializing.Formatting;
/// <summary>
/// A static class to parse containsers from human readable WinterForge syntax
/// </summary>
internal static class ContainerParser
{
    public static bool ParseContainers(HumanReadableParser parser, StreamWriter output)
    {
        if (output == null) throw new ArgumentNullException(nameof(output));

        string? line = parser.currentLine;
        while (string.IsNullOrWhiteSpace(line))
        {
            line = parser.ReadLine();
            if (line == null) break; // EOF
            line = line.Trim();
        }

        if (line == null) return true;

        if (line.StartsWith("#container"))
        {
            string containerHeader = line["#container".Length..].Trim();
            string containerName;
            bool openBraceOnSameLine = false;

            // header may include a trailing '{'
            if (containerHeader.EndsWith("{"))
            {
                containerName = containerHeader[..^1].Trim();
                openBraceOnSameLine = true;
            }
            else
            {
                containerName = containerHeader;
            }

            WriteOpcode(output, OpCode.CONTAINER_START, containerName);

            // consume opening brace if not present on same line
            if (!openBraceOnSameLine)
            {
                SeekToNextNonEmptyLineExpecting("{", parser);
            }

            // Now read the entire container block
            List<string> containerBlock = ReadBlockContent(parser);

            ProcessContainerBlock(containerName, containerBlock, output, parser);

            WriteOpcode(output, OpCode.CONTAINER_END, containerName);
        }
        else if (line.StartsWith("#template"))
        {
            // Top-level single-template parse
            string header = line["#template".Length..].Trim();
            bool hasBrace = header.EndsWith("{");
            if (hasBrace) header = header[..^1].Trim();

            if (!hasBrace)
            {
                // Expect an opening brace on the next non-empty line
                SeekToNextNonEmptyLineExpecting("{", parser);
            }

            // Read the body of the template (inner lines)
            List<string> body = ReadBlockContent(parser);
            // Keep consistent with other callers that append a trailing "}" token
            body.Add("}");

            // Reuse the public ParseTemplate helper
            ParseTemplate(parser, header, body, output);
        }

        return true;
    }

    // Processes lines inside a container (variables, constructors, templates, other blocks)
    private static void ProcessContainerBlock(string containerName, List<string> blockLines, StreamWriter output, HumanReadableParser parser)
    {
        int lineIndex = 0;
        while (lineIndex < blockLines.Count)
        {
            string rawLine = blockLines[lineIndex].Trim();
            if (rawLine.Length == 0) { lineIndex++; continue; }

            if (rawLine.StartsWith("#variables"))
            {
                // Advance to opening brace if needed and read its inner lines
                if (!rawLine.EndsWith("{"))
                {
                    // skip the following line which should be '{'
                    lineIndex++;
                }

                // read variables block
                List<string> varLines = ExtractSubBlock(blockLines, ref lineIndex);
                foreach (var vline in varLines)
                {
                    ParseVariableLine(vline, output, parser);
                }

                continue;
            }

            if (TryParseBlock(rawLine, blockLines, ref lineIndex, containerName))
                continue;

            lineIndex++;
        }

        bool TryParseBlock(string rawLine, List<string> blockLines, ref int lineIndex, string containerName = null)
        {
            // Determine if this line is a block header (constructor or template)
            string header = null;
            string blockTypeName = null;
            bool isConstructor = false;
            List<(string type, string name)> paramTuples = new();

            if (containerName != null && rawLine.StartsWith(containerName) &&
                (rawLine.Length == containerName.Length || rawLine[containerName.Length] == ' ' || rawLine[containerName.Length] == '{'))
            {
                // It's a constructor
                blockTypeName = containerName;
                header = rawLine[containerName.Length..].Trim();
                isConstructor = true;
            }
            else if (rawLine.StartsWith("#template"))
            {
                // It's a template
                header = rawLine["#template".Length..].Trim();
                blockTypeName = "#template"; // just for clarity
            }
            else
            {
                return false; // not a block we care about
            }

            bool hasBraceOnHeader = header.EndsWith("{");
            if (hasBraceOnHeader) header = header[..^1].Trim();

            // Parse name and parameters
            List<string> headerTokens = TokenizeHeader(header);
            string blockName = isConstructor ? containerName : headerTokens[0];
            if (headerTokens.Count > 1)
                paramTuples = ParseParamTuples(headerTokens.Skip(1).ToList());

            // Extract body
            List<string> body = ExtractSubBlock(blockLines, ref lineIndex, headerLine: rawLine);
            body.Add("}");

            // Emit opcodes (reuse ParseTemplate/ParseConstructor to avoid duplication)
            if (blockTypeName == "#template")
                ParseTemplate(parser, blockName, paramTuples, body, output);
            else
                ParseConstructor(parser, blockName, paramTuples, body, output);

            return true;
        }
    }


    // Parses a variable declaration line (e.g. "x;" or "y = 5;") and emits VAR_DEF opcodes.
    private static void ParseVariableLine(string line, StreamWriter output, HumanReadableParser parser)
    {
        string trimmed = line.Trim();
        if (trimmed.EndsWith(";")) trimmed = trimmed[..^1].Trim();

        if (trimmed.Length == 0) return;

        if (trimmed.Contains('='))
        {
            var parts = trimmed.Split(new[] { '=' }, 2);
            string name = parts[0].Trim();
            string expr = parts[1].Trim();
            WriteOpcode(output, OpCode.VAR_DEF_START, name);
            if (expr.Contains("->"))
            {
                parser.ParseRHSAccess(expr, null, true);
            }
            else
                WriteOpcode(output, OpCode.VAR_DEFAULT_VALUE, expr); // put value as raw since its not an access expression. VM will handle this raw value

            WriteOpcode(output, OpCode.VAR_DEF_END, name);
        }
        else
        {
            WriteOpcode(output, OpCode.VAR_DEF_START, trimmed);
            WriteOpcode(output, OpCode.VAR_DEF_END, trimmed);
        }
    }

    /// <summary>
    /// Public helper to parse a single template. Header is the content after "#template" (e.g. "Foo int n").
    /// bodyLines should be the inner lines (ReadBlockContent result) + typically a trailing "}" if caller expects it.
    /// This emits TEMPLATE_CREATE, enqueues the body into the parser and emits TEMPLATE_END.
    /// </summary>
    public static void ParseTemplate(HumanReadableParser parser, string header, List<string> bodyLines, StreamWriter output)
    {
        // header may be like "Foo int n" or "Foo" (no params)
        List<string> headerTokens = TokenizeHeader(header);
        if (headerTokens.Count == 0) throw new InvalidDataException("Template header empty.");

        string templateName = headerTokens[0];
        var paramTuples = headerTokens.Count > 1 ? ParseParamTuples(headerTokens.Skip(1).ToList()) : new List<(string type, string name)>();

        ParseTemplate(parser, templateName, paramTuples, bodyLines, output);
    }

    /// <summary>
    /// Overload accepting already-parsed name + param tuples (avoids re-tokenizing).
    /// </summary>
    public static void ParseTemplate(HumanReadableParser parser, string templateName, List<(string type, string name)> paramTuples, List<string> bodyLines, StreamWriter output)
    {
        // prepare opcode args: name, paramCount, [type, name]...
        var args = new List<string> { templateName, paramTuples.Count.ToString() };
        foreach (var p in paramTuples)
        {
            args.Add(p.type);
            args.Add(p.name);
        }

        WriteOpcode(output, OpCode.TEMPLATE_CREATE, args.ToArray());

        // give the template body to the parser (so ParseBlock/ParseObjectOrAssignment will run)
        parser.EnqueueLines(bodyLines);
        parser.ContinueWithBlock(null, true);

        WriteOpcode(output, OpCode.TEMPLATE_END, templateName);
    }

    /// <summary>
    /// Parse a single constructor (container-named block) given parsed param tuples.
    /// </summary>
    public static void ParseConstructor(HumanReadableParser parser, string constructorName, List<(string type, string name)> paramTuples, List<string> bodyLines, StreamWriter output)
    {
        var args = new List<string> { constructorName, paramTuples.Count.ToString() };
        foreach (var p in paramTuples)
        {
            args.Add(p.type);
            args.Add(p.name);
        }

        WriteOpcode(output, OpCode.CONSTRUCTOR_START, args.ToArray());

        parser.EnqueueLines(bodyLines);
        parser.ContinueWithBlock(null, true);

        WriteOpcode(output, OpCode.CONSTRUCTOR_END, constructorName);
    }

    // Reads a block's inner lines including nested braces. Caller must be positioned AFTER the initial opening brace.
    // This reads until the matching closing brace for the first opening brace encountered.
    private static List<string> ReadBlockContent(HumanReadableParser parser)
    {
        var result = new List<string>();
        int depth = 1; // caller consumed the initial opening brace
        string? line;
        while ((line = parser.ReadLine()) != null)
        {
            string trimmed = line;
            // We must count braces in the raw line (handles same-line braces)
            for (int idx = 0; idx < trimmed.Length; idx++)
            {
                char c = trimmed[idx];
                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    // if depth hits 0, we should capture remaining text on this line up to the brace (if any)
                    if (depth == 0)
                    {
                        // capture portion before this brace (if non-empty)
                        string before = trimmed[..idx].TrimEnd();
                        if (before.Length > 0) result.Add(before);
                        return result;
                    }
                }
            }

            // if no closing to end the block on this line, add full line
            result.Add(trimmed);
        }

        throw new EndOfStreamException("Unexpected end of input while reading a block.");
    }

    // Extracts a subblock starting at the current index of the container block lines.
    // Advances index to the line after the subblock end. headerLine can be provided if the header line already contains data (e.g. "Name {" on same line).
    private static List<string> ExtractSubBlock(List<string> blockLines, ref int index, string? headerLine = null)
    {
        // If headerLine contains a '{' at the end, start from next line; else advance until we find the line with '{'
        if (headerLine != null && headerLine.Trim().EndsWith("{"))
        {
            // header had brace; start reading the block from the next line
            index++; // move to first inner line
        }
        else
        {
            // find the opening brace line
            while (index < blockLines.Count && !blockLines[index].Trim().Contains("{"))
            {
                index++;
            }

            if (index < blockLines.Count && blockLines[index].Trim().Contains("{"))
            {
                // Move index to the line after the opening brace
                index++;
            }
            else
            {
                throw new InvalidDataException("Expected '{' to start sub-block.");
            }
        }

        // Now read lines, tracking nested braces
        var collected = new List<string>();
        int depth = 1;
        while (index < blockLines.Count && depth > 0)
        {
            string current = blockLines[index];
            // Count braces in the current line
            for (int i = 0; i < current.Length; i++)
            {
                char c = current[i];
                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        // capture text before this closing brace (if any)
                        string before = current[..i].TrimEnd();
                        if (before.Length > 0) collected.Add(before);
                        index++; // advance past closing brace and return
                        return collected;
                    }
                }
            }

            collected.Add(current);
            index++;
        }

        if (depth != 0) throw new InvalidDataException("Unbalanced braces in sub-block.");
        return collected;
    }

    // Splits a header into tokens while preserving textual tokens.
    private static List<string> TokenizeHeader(string header)
    {
        var tokens = new List<string>();
        if (string.IsNullOrWhiteSpace(header)) return tokens;
        var parts = header.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts) tokens.Add(p.Trim());
        return tokens;
    }

    // Accepts header tokens after the template name and returns list of (type,name) pairs.
    // Expects alternating type name pairs. If odd number, the last type with missing name will produce a generated name ("argN").
    private static List<(string type, string name)> ParseParamTuples(List<string> tokens)
    {
        var result = new List<(string type, string name)>();
        int i = 0;
        int argIndex = 0;
        while (i < tokens.Count)
        {
            string typeToken = tokens[i++];
            string nameToken;
            if (i < tokens.Count)
            {
                nameToken = tokens[i++];
            }
            else
            {
                nameToken = "arg" + argIndex++;
            }

            result.Add((typeToken, nameToken));
        }
        return result;
    }

    // helper: writes a single opcode line with arguments, quoting an arg if it contains spaces.
    private static void WriteOpcode(StreamWriter stream, OpCode opcode, params string[] args)
    {
        var all = new List<string> { ((byte)opcode).ToString() };
        foreach (var a in args)
        {
            if (a == null) all.Add("\"\"");
            else all.Add(QuoteArg(a));
        }

        string line = string.Join(" ", all);
        stream.WriteLine(line);
    }

    // Quote an argument if it contains spaces or special characters; also escapes inner quotes and backslashes.
    private static string QuoteArg(string value)
    {
        if (value == null) return "\"\"";
        bool needsQuoting = value.Any(c => char.IsWhiteSpace(c)) || value.Contains("\"") || value.Contains("\\");
        if (!needsQuoting) return value;
        var escaped = value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        return "\"" + escaped + "\"";
    }

    // Advances the reader until it finds a non-empty line that equals expected. Throws if not found.
    private static void SeekToNextNonEmptyLineExpecting(string expected, HumanReadableParser parser)
    {
        string? l;
        while ((l = parser.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(l)) continue;
            if (l.Trim() == expected) return;
            throw new InvalidDataException($"Expected '{expected}' but found '{l.Trim()}'");
        }

        throw new EndOfStreamException($"Expected '{expected}' but reached end of stream.");
    }
}
