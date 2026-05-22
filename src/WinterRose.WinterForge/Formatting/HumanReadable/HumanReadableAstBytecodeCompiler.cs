using System.Globalization;
using System.Text;
using WinterRose.WinterForgeSerializing.Compiling;
using WinterRose.WinterForgeSerializing.Expressions;
using WinterRose.WinterForgeSerializing.Formatting.HumanReadable.Parsing;
using WinterRose.WinterForgeSerializing.Instructions;

namespace WinterRose.WinterForgeSerializing.Formatting;

internal static class HumanReadableAstBytecodeCompiler
{
    public static void Compile(HumanReadableProgramNode program, string source, Stream output)
    {
        using BinaryWriter writer = new(output, Encoding.UTF8, leaveOpen: true);
        Compiler compiler = new(source, writer);
        compiler.CompileProgram(program);
        writer.Flush();
    }

    public static void Compile(HumanReadableProgramNode program, string source, Stream output, CompilationOptions options)
    {
        using BinaryWriter writer = new(output, Encoding.UTF8, leaveOpen: true);
        Compiler compiler = new(source, writer, options);
        compiler.CompileProgram(program);
        writer.Flush();
    }
}

internal sealed class Compiler
{
    private readonly string source;
    private readonly BinaryWriter writer;
    private readonly Dictionary<string, int> aliasMap = [];
    private readonly HashSet<string> variables = [];
    private readonly List<(string start, string end)> flowLabels = [];
    private int autoAsIDs;
    private readonly CompilationOptions options;

    private static readonly Dictionary<OpCode, byte> opcodeMap = Enum
        .GetValues<OpCode>()
        .ToDictionary(op => op, op => (byte)op);

    public Compiler(string source, BinaryWriter writer)
    {
        this.source = source;
        this.writer = writer;
        options = CompilationOptions.Default;
    }

    public Compiler(string source, BinaryWriter writer, CompilationOptions options)
    {
        this.source = source;
        this.writer = writer;
        this.options = options;
    }

    public void CompileProgram(HumanReadableProgramNode program)
    {
        CompileStatements(program.Statements, false, null);
    }

    private void CompileStatements(IReadOnlyList<HumanReadableStatementNode> statements, bool isBody, string? currentObjectId)
    {
        for (int i = 0; i < statements.Count; i++)
        {
            var stmt = statements[i];
            string text = HeaderText(stmt);
            if (string.IsNullOrWhiteSpace(text) || text.StartsWith("//", StringComparison.Ordinal))
                continue;

            if (text.StartsWith("if ", StringComparison.Ordinal))
            {
                CompileIfChain(statements, ref i, isBody, currentObjectId);
                continue;
            }

            CompileStatement(stmt, isBody, currentObjectId);
        }
    }

    private void CompileStatement(HumanReadableStatementNode stmt, bool isBody, string? currentObjectId)
    {
        string line = HeaderText(stmt);

        if (line.EndsWith(':') && !line.Contains('='))
        {
            EmitLabel(line[..^1].Trim());
            return;
        }

        if (line.StartsWith("goto ", StringComparison.Ordinal))
        {
            string label = line["goto".Length..].Trim().TrimEnd(';');
            EmitJump(label);
            return;
        }

        if (line.StartsWith("#import", StringComparison.OrdinalIgnoreCase))
        {
            ParseImport(line, stmt);
            return;
        }

        if (line.StartsWith("#container", StringComparison.Ordinal))
        {
            ParseContainer(stmt);
            return;
        }

        if (line.StartsWith("#template", StringComparison.Ordinal))
        {
            ParseTemplate(stmt);
            return;
        }

        if (line.StartsWith("while", StringComparison.Ordinal))
        {
            ParseWhile(stmt, isBody, currentObjectId);
            return;
        }

        if (line.StartsWith("for ", StringComparison.Ordinal))
        {
            ParseFor(stmt, isBody, currentObjectId);
            return;
        }

        if (TryParseObjectDefinition(stmt, isBody, currentObjectId))
            return;

        if (line.StartsWith("var ", StringComparison.Ordinal) && HRPHelpers.ContainsSequenceOutsideQuotes(line, "=") is int eqI)
        {
            ParseVarCreation(line, eqI, isBody, currentObjectId);
            return;
        }

        if (line.StartsWith("global ", StringComparison.Ordinal) && HRPHelpers.ContainsSequenceOutsideQuotes(line, "=") is int eqI2)
        {
            ParseGlobalVarCreation(line, eqI2, isBody, currentObjectId);
            return;
        }

        if (line.StartsWith("return", StringComparison.Ordinal))
        {
            HandleReturn(line, isBody, currentObjectId);
            return;
        }

        if (line.StartsWith("as", StringComparison.Ordinal))
        {
            string idRaw = line[2..].Trim().TrimEnd(';');
            if (idRaw == "nextid")
                idRaw = GetAutoID().ToString(CultureInfo.InvariantCulture);

            EmitInt(OpCode.AS, int.Parse(idRaw, CultureInfo.InvariantCulture));
            return;
        }

        if (line.StartsWith("alias", StringComparison.Ordinal))
        {
            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int id = int.Parse(parts[1], CultureInfo.InvariantCulture);
            string alias = parts[^1].TrimEnd(';');
            aliasMap[alias] = id;
            return;
        }

        if (line.StartsWith("continue", StringComparison.Ordinal) && flowLabels.Count > 0)
        {
            string countStr = line["continue".Length..].Trim().TrimEnd(';');
            int count = int.TryParse(countStr, out int c) ? c : 1;
            EmitJump(flowLabels[^count].start);
            return;
        }

        if (line.StartsWith("break", StringComparison.Ordinal) && flowLabels.Count > 0)
        {
            string countStr = line["break".Length..].Trim().TrimEnd(';');
            int count = int.TryParse(countStr, out int c) ? c : 1;
            EmitJump(flowLabels[^count].end);
            return;
        }

        if (HRPHelpers.HasValidGenericFollowedByBracket(line))
        {
            string noSemicolon = line.TrimEnd(';').Trim();
            int eqIndex = noSemicolon.IndexOf('=');
            int ltIndex = noSemicolon.IndexOf('<');

            if (eqIndex != -1 && ltIndex != -1 && eqIndex < ltIndex)
            {
                string field = noSemicolon[..eqIndex].Trim();
                string rhs = noSemicolon[(eqIndex + 1)..].Trim();
                ParseCollectionValue(rhs, isBody, currentObjectId);
                EmitSet(field, "#stack()");
            }
            else
            {
                ParseCollectionValue(noSemicolon, isBody, currentObjectId);
            }

            return;
        }

        if (HRPHelpers.ContainsExpressionOutsideQuotes(line) && line.Contains(" = ", StringComparison.Ordinal) && line.EndsWith(';'))
        {
            ParseAssignment(line, isBody, currentObjectId);
            return;
        }

        if (line.Contains("->", StringComparison.Ordinal))
        {
            HandleAccessing(line, isBody, currentObjectId);
            return;
        }

        if (line.IndexOf(':') is int colinx && line.IndexOf('=') is int eqinx && colinx is not -1 && eqinx is not -1 && colinx < eqinx)
        {
            ParseAnonymousAssignment(line, isBody, currentObjectId);
            return;
        }

        if (line.Contains('=') && (line.EndsWith(';') || line.Contains('"')))
        {
            ParseAssignment(line, isBody, currentObjectId);
            return;
        }

        if (HRPHelpers.ContainsSequenceOutsideQuotes(line, "(") != -1 && HRPHelpers.EndsWithParenOrParenSemicolon(line))
        {
            ParseMethodCall(currentObjectId, line.TrimEnd(';'), isBody);
            EmitNoArgs(OpCode.VOID_STACK_ITEM);
            return;
        }

        throw Error("Unexpected statement.", stmt, line);
    }

    private bool TryParseObjectDefinition(HumanReadableStatementNode stmt, bool isBody, string? currentObjectId)
    {
        string line = HeaderText(stmt);
        string firstLine = line.Split("\n").First().Trim();
        bool hasColon = HRPHelpers.ContainsSequenceOutsideQuotes(firstLine, ":") != -1;
        bool hasParens = line.Contains('(') && line.Contains(')');
        bool hasSemicolon = line.EndsWith(';');

        if (!hasColon)
            return false;

        int colonIndex = line.IndexOf(':');
        if (colonIndex <= 0)
            return false;

        string left = line[..colonIndex].Trim();
        string right = line[(colonIndex + 1)..].Trim().TrimEnd(';');

        string type;
        int ctorArgCount = 0;

        if (hasParens)
        {
            int open = left.IndexOf('(');
            int close = left.LastIndexOf(')');
            if (open < 0 || close < open)
                throw Error("Invalid constructor definition.", stmt, line);

            type = left[..open].Trim();
            string argList = left[(open + 1)..close].Trim();
            var args = string.IsNullOrWhiteSpace(argList)
                ? []
                : HRPHelpers.SplitPreserveQuotesAndParentheses(argList, ',');

            for (int i = 0; i < args.Count; i++)
            {
                string v = ValidateValue(args[i].Trim(), isBody, currentObjectId);
                if (v != "#stack()")
                    EmitPush(v);
            }

            ctorArgCount = args.Count;
        }
        else
        {
            type = left;
        }

        if (type.Contains("Anonymous", StringComparison.Ordinal))
            type = type.Replace(' ', '-');

        string idRaw = right;
        if (idRaw is "temp" or "nextid")
            idRaw = GetAutoID().ToString(CultureInfo.InvariantCulture);

        int assignedId;
        bool hasNumericId = int.TryParse(idRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int numericId);
        if (hasNumericId)
        {
            assignedId = numericId;
        }
        else
        {
            assignedId = GetAutoID();
        }

        EmitDefine(type, assignedId, ctorArgCount);

        if (stmt.IsBlock)
        {
            CompileStatements(stmt.Children, isBody, assignedId.ToString(CultureInfo.InvariantCulture));
            EmitInt(OpCode.END, assignedId);
        }
        else
        {
            EmitInt(OpCode.END, assignedId);
        }

        if (!hasNumericId)
        {
            bool isGlobal = false;
            if (idRaw.StartsWith("global", StringComparison.Ordinal))
            {
                idRaw = idRaw["global".Length..].Trim();
                isGlobal = true;
            }

            if (!isGlobal)
            {
                EmitSingleString(OpCode.FORCE_DEF_VAR, idRaw);
                EmitSet(idRaw, $"#ref({assignedId})");
                variables.Add(idRaw);
            }
            else
            {
                aliasMap[idRaw] = assignedId;
            }
        }

        return true;
    }

    private void ParseImport(string line, HumanReadableStatementNode stmt)
    {
        string rest = line["#import".Length..].Trim();
        if (!rest.StartsWith('"'))
            throw Error("Import must start with string path.", stmt, line);

        int endQuote = rest.IndexOf('"', 1);
        if (endQuote < 1)
            throw Error("Import path string not closed.", stmt, line);

        string fileName = rest[1..endQuote];
        rest = rest[(endQuote + 1)..].Trim();

        int id = GetAutoID();
        if (rest.StartsWith("as", StringComparison.Ordinal))
        {
            rest = rest[2..].Trim();
            int aliasEnd = rest.IndexOfAny([' ', ';']);
            if (aliasEnd == -1)
                aliasEnd = rest.Length;

            string alias = rest[..aliasEnd].Trim();
            aliasMap[alias] = id;
            rest = rest[aliasEnd..].Trim();
        }

        if (rest.StartsWith("(compiles into ", StringComparison.Ordinal))
        {
            if (!rest.EndsWith(')'))
                throw Error("Import compile statement not closed with ')'", stmt, line);

            string outputPath = rest[15..^1].Trim();
            WinterForge.ConvertFromFileToFile(fileName, outputPath);
            fileName = outputPath;
        }

        EmitImport(fileName, id);
    }

    private void ParseContainer(HumanReadableStatementNode stmt)
    {
        string header = HeaderText(stmt);
        string containerName = header["#container".Length..].Trim();
        EmitSingleString(OpCode.CONTAINER_START, containerName);

        var children = stmt.Children;
        for (int i = 0; i < children.Count; i++)
        {
            var child = children[i];
            string line = HeaderText(child);
            if (line.StartsWith("#variables", StringComparison.Ordinal))
            {
                if (!child.IsBlock)
                    throw Error("#variables must be a block.", child, line);

                foreach (var varStmt in child.Children)
                    ParseContainerVariable(varStmt);
                continue;
            }

            if (line.StartsWith("#template", StringComparison.Ordinal))
            {
                ParseTemplate(child);
                continue;
            }

            if (line.StartsWith(containerName, StringComparison.Ordinal))
            {
                ParseContainerConstructor(containerName, child);
                continue;
            }

            throw Error("Unknown line in container definition.", child, line);
        }

        EmitSingleString(OpCode.CONTAINER_END, containerName);
    }

    private void ParseContainerVariable(HumanReadableStatementNode stmt)
    {
        string line = HeaderText(stmt).TrimEnd(';').Trim();
        if (string.IsNullOrWhiteSpace(line))
            return;

        if (line.Contains('='))
        {
            string[] parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
            EmitSingleString(OpCode.VAR_DEF_START, parts[0]);
            EmitVarDefault(parts[1]);
            EmitSingleString(OpCode.VAR_DEF_END, parts[0]);
        }
        else
        {
            EmitSingleString(OpCode.VAR_DEF_START, line);
            EmitSingleString(OpCode.VAR_DEF_END, line);
        }
    }

    private void ParseContainerConstructor(string containerName, HumanReadableStatementNode stmt)
    {
        string header = HeaderText(stmt);
        string raw = header[containerName.Length..].Trim();
        var parsed = ParseCallableHeader(raw, containerName);
        EmitCallableStart(OpCode.CONSTRUCTOR_START, parsed.name, parsed.parameters);
        if (stmt.IsBlock)
            CompileStatements(stmt.Children, true, null);
        EmitSingleString(OpCode.CONSTRUCTOR_END, parsed.name);
    }

    private void ParseTemplate(HumanReadableStatementNode stmt)
    {
        string header = HeaderText(stmt)["#template".Length..].Trim();
        var parsed = ParseCallableHeader(header, "template");
        EmitCallableStart(OpCode.TEMPLATE_CREATE, parsed.name, parsed.parameters);
        if (stmt.IsBlock)
            CompileStatements(stmt.Children, true, null);
        EmitSingleString(OpCode.TEMPLATE_END, parsed.name);
    }

    private (string name, List<(string type, string name)> parameters) ParseCallableHeader(string header, string fallbackName)
    {
        var tokens = header.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string name = tokens.Length == 0 ? fallbackName : tokens[0];

        List<(string type, string name)> parameters = [];
        for (int i = 1; i + 1 < tokens.Length; i += 2)
            parameters.Add((tokens[i], tokens[i + 1]));

        return (name, parameters);
    }

    private void ParseWhile(HumanReadableStatementNode stmt, bool isBody, string? currentObjectId)
    {
        string expr = HeaderText(stmt)["while".Length..].Trim();
        string labelStart = "WHILE" + GetAutoID();
        string labelEnd = "WHILE" + GetAutoID();
        flowLabels.Add((labelStart, labelEnd));

        EmitLabel(labelStart);
        string cond = ValidateValue(expr, isBody, currentObjectId);
        if (cond != "#stack()")
            EmitPush(cond);

        EmitSingleString(OpCode.JUMP_IF_FALSE, labelEnd);
        EmitNoArgs(OpCode.SCOPE_PUSH);

        if (stmt.IsBlock)
            CompileStatements(stmt.Children, isBody, currentObjectId);

        EmitNoArgs(OpCode.SCOPE_POP);
        EmitJump(labelStart);
        EmitLabel(labelEnd);
        flowLabels.RemoveAt(flowLabels.Count - 1);
    }

    private void ParseFor(HumanReadableStatementNode stmt, bool isBody, string? currentObjectId)
    {
        string content = HeaderText(stmt)["for".Length..].Trim();
        string[] expressions = HRPHelpers.SplitForLoopContent(content);
        if (expressions.Length < 3)
            throw Error("Invalid for loop syntax.", stmt, content);

        string labelStart = "FOR" + GetAutoID();
        string labelEnd = "FOR" + GetAutoID();
        flowLabels.Add((labelStart, labelEnd));

        EmitNoArgs(OpCode.SCOPE_PUSH);

        if (!string.IsNullOrWhiteSpace(expressions[0]))
            ParseInlineExpressionAsStatement(expressions[0], isBody, currentObjectId);

        EmitLabel(labelStart);
        if (string.IsNullOrWhiteSpace(expressions[1]))
            throw Error("For loop requires condition expression.", stmt, content);

        string cond = ValidateValue(expressions[1], isBody, currentObjectId);
        if (cond != "#stack()")
            EmitPush(cond);

        EmitSingleString(OpCode.JUMP_IF_FALSE, labelEnd);

        if (stmt.IsBlock)
            CompileStatements(stmt.Children, isBody, currentObjectId);

        if (!string.IsNullOrWhiteSpace(expressions[2]))
            ParseInlineExpressionAsStatement(expressions[2], isBody, currentObjectId);

        EmitJump(labelStart);
        EmitLabel(labelEnd);
        EmitNoArgs(OpCode.SCOPE_POP);
        flowLabels.RemoveAt(flowLabels.Count - 1);
    }

    private void CompileIfChain(IReadOnlyList<HumanReadableStatementNode> statements, ref int index, bool isBody, string? currentObjectId)
    {
        string endLabel = "L" + GetAutoID();

        while (index < statements.Count)
        {
            var stmt = statements[index];
            string raw = HeaderText(stmt);

            bool isIf = raw.StartsWith("if ", StringComparison.Ordinal);
            bool isElseIf = raw.StartsWith("else if ", StringComparison.Ordinal);
            bool isElse = raw == "else" || raw.StartsWith("else ", StringComparison.Ordinal);

            if (!isIf && !isElseIf && !isElse)
                break;

            bool hasCondition = isIf || isElseIf;
            string nextBranchLabel = "L" + GetAutoID();

            if (hasCondition)
            {
                int kw = isIf ? 2 : 7;
                string expr = raw[kw..].Trim();
                string cond = ValidateValue(expr, isBody, currentObjectId);
                if (cond != "#stack()")
                    EmitPush(cond);
                EmitSingleString(OpCode.JUMP_IF_FALSE, nextBranchLabel);
            }

            EmitNoArgs(OpCode.SCOPE_PUSH);
            if (stmt.IsBlock)
                CompileStatements(stmt.Children, isBody, currentObjectId);
            EmitNoArgs(OpCode.SCOPE_POP);

            if (hasCondition)
                EmitJump(endLabel);

            EmitLabel(nextBranchLabel);

            int next = index + 1;
            if (next >= statements.Count)
                break;

            string nextHeader = HeaderText(statements[next]);
            if (nextHeader.StartsWith("else if ", StringComparison.Ordinal) || nextHeader == "else" || nextHeader.StartsWith("else ", StringComparison.Ordinal))
            {
                index = next;
                continue;
            }

            break;
        }

        EmitLabel(endLabel);
    }

    private void ParseInlineExpressionAsStatement(string statement, bool isBody, string? currentObjectId)
    {
        string line = statement.Trim();
        if (line.Contains("=", StringComparison.Ordinal))
        {
            ParseAssignment(line + ';', isBody, currentObjectId);
            return;
        }

        if (line.Contains("->", StringComparison.Ordinal))
        {
            HandleAccessing(line, isBody, currentObjectId);
            return;
        }

        if (HRPHelpers.ContainsSequenceOutsideQuotes(line, "(") != -1 && HRPHelpers.EndsWithParenOrParenSemicolon(line))
        {
            ParseMethodCall(currentObjectId, line.TrimEnd(';'), isBody);
            EmitNoArgs(OpCode.VOID_STACK_ITEM);
            return;
        }

        ValidateValue(line, isBody, currentObjectId);
    }

    private void ParseVarCreation(string line, int eqI, bool isBody, string? currentObjectId)
    {
        if (eqI == -1 && line.EndsWith(';'))
        {
            eqI = line.Length - 1;
            line += "null;";
        }

        string varName = line[4..eqI].Trim();
        string rhs = line[(eqI + 1)..].Trim().TrimEnd(';');
        string value = ValidateValue(rhs, isBody, currentObjectId);
        EmitSingleString(OpCode.FORCE_DEF_VAR, varName);
        EmitSet(varName, value);
        variables.Add(varName);
    }

    private void ParseGlobalVarCreation(string line, int eqI, bool isBody, string? currentObjectId)
    {
        string varName = line["global".Length..eqI].Trim();
        string rhs = line[(eqI + 1)..].Trim().TrimEnd(';');
        int nextId = GetAutoID();
        string value = ValidateValue(rhs, isBody, currentObjectId);
        if (value != "#stack()")
            EmitPush(value);

        EmitInt(OpCode.AS, nextId);
        aliasMap[varName] = nextId;
    }

    private void HandleReturn(string line, bool isBody, string? currentObjectId)
    {
        string raw = NormalizeBuiltins(line[6..].Trim().TrimEnd(';').Trim());
        if (string.IsNullOrWhiteSpace(raw))
            raw = "null";

        string ret;
        if (isBody)
        {
            ret = ValidateValue(raw, isBody, currentObjectId);
        }
        else
        {
            if (aliasMap.TryGetValue(raw, out int aliasId))
                raw = aliasId.ToString(CultureInfo.InvariantCulture);

            if (raw.All(char.IsDigit))
                raw = $"#ref({raw})";

            if (HRPHelpers.ContainsSequenceOutsideQuotes(raw, "->") != -1)
            {
                HandleAccessing(raw, isBody, currentObjectId, allowNoRHS: true);
                raw = "#stack()";
            }

            ret = raw;
        }

        EmitReturn(ret);
    }

    private void ParseAnonymousAssignment(string line, bool isBody, string? currentObjectId)
    {
        int eq = line.IndexOf('=');
        if (eq == -1)
            throw new WinterForgeFormatException("Invalid anonymous assignment format");

        string typeAndName = line[..eq].Trim();
        string value = line[(eq + 1)..].Trim().TrimEnd(';').Trim();

        string[] parts = typeAndName.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            throw new WinterForgeFormatException("Invalid anonymous assignment format, expected 'type:name'");

        string parsed = ValidateValue(value, isBody, currentObjectId);
        EmitAnonymousSet(parts[0], parts[1], parsed);
    }

    private void ParseAssignment(string line, bool isBody, string? currentObjectId)
    {
        string clean = line.TrimEnd(';');
        int eq = clean.IndexOf('=');
        string field = clean[..eq].Trim();
        string value = clean[(eq + 1)..].Trim();
        string parsed = ValidateValue(value, isBody, currentObjectId);
        EmitSet(field, parsed);
    }

    private void HandleAccessing(string line, bool isBody, string? currentObjectId, bool allowNoRHS = false)
    {
        string[] assignmentParts = line.Split('=', 2, StringSplitOptions.TrimEntries);
        string accessPart = assignmentParts[0];
        string? rhs = assignmentParts.Length > 1 ? assignmentParts[1].Trim().TrimEnd(';') : null;

        if (rhs is null && !(accessPart.Contains('(') && accessPart.Contains(')')) && !allowNoRHS)
            throw new WinterForgeFormatException("Missing right-hand side for assignment and left-hand side is not a function call.");

        string val = "";
        if (rhs != null && rhs.Contains("->", StringComparison.Ordinal))
            ParseRhsAccess(rhs, currentObjectId, isBody);
        else if (rhs != null)
            val = ValidateValue(rhs, isBody, currentObjectId);

        var lhsParts = HRPHelpers.SplitPreserveParentheses(accessPart);
        if (lhsParts.Count == 0)
            return;

        string first = lhsParts[0];
        if (lhsParts.Count == 1)
        {
            if (rhs is null)
            {
                if (first.Contains('(') && first.Contains(')'))
                    return;
                throw new WinterForgeFormatException("Missing right-hand side for assignment.");
            }

            EmitSet(first, "#stack()");
            return;
        }

        PushReferenceLike(first, currentObjectId);

        for (int i = 1; i < lhsParts.Count; i++)
        {
            string part = lhsParts[i];
            if (string.IsNullOrWhiteSpace(part))
                continue;

            bool isLast = i == lhsParts.Count - 1;
            if (part.Contains('(') && part.Contains(')'))
            {
                if (rhs != null)
                    throw new WinterForgeFormatException("Left hand side function is illegal when used as an lvalue for assignment.");
                ParseMethodCall(currentObjectId, part, isBody);
                continue;
            }

            if (rhs != null && isLast)
                EmitSetAccess(part, val);
            else
                EmitSingleString(OpCode.ACCESS, part);
        }
    }

    private void ParseRhsAccess(string rhs, string? currentObjectId, bool isBody)
    {
        var rhsParts = HRPHelpers.SplitPreserveParentheses(rhs);
        if (rhsParts.Count == 0)
            throw new WinterForgeFormatException("nothing to access on the right side...");

        PushReferenceLike(rhsParts[0], currentObjectId);
        for (int i = 1; i < rhsParts.Count; i++)
        {
            string part = rhsParts[i].Trim().TrimEnd(';');
            if (string.IsNullOrWhiteSpace(part))
                continue;

            if (part.Contains('(') && part.Contains(')'))
                ParseMethodCall(currentObjectId, part, isBody);
            else
                EmitSingleString(OpCode.ACCESS, part);
        }
    }

    private void ParseMethodCall(string? currentObjectId, string part, bool isBody)
    {
        int openParen = part.IndexOf('(');
        int closeParen = part.LastIndexOf(')');
        if (openParen < 0 || closeParen < openParen)
            throw new WinterForgeFormatException($"Invalid method call syntax: {part}");

        string methodName = part[..openParen].Trim();
        if (aliasMap.TryGetValue(methodName, out int aliasRef))
            methodName = $"#ref({aliasRef})";

        string argList = part[(openParen + 1)..closeParen];
        var args = HRPHelpers.SplitPreserveQuotesAndParentheses(argList, ',');
        for (int i = args.Count - 1; i >= 0; i--)
        {
            string arg = args[i].Trim();
            if (arg == "..")
                continue;

            if (arg.Contains("->", StringComparison.Ordinal))
                HandleAccessing(arg, isBody, currentObjectId, allowNoRHS: true);
            else if (HRPHelpers.ContainsExpressionOutsideQuotes(arg))
                ParseExpression(arg, currentObjectId, isBody);
            else if (aliasMap.TryGetValue(arg, out int aid))
                EmitPush($"#ref({aid})");
            else
                EmitPush(arg);
        }

        EmitCall(methodName, args.Count);
    }

    private string ParseCollectionValue(string value, bool isBody, string? currentObjectId)
    {
        int typeOpen = value.IndexOf('<');
        int blockOpen = value.IndexOf('[');
        int blockClose = value.LastIndexOf(']');

        if (typeOpen == -1 || blockOpen == -1 || blockClose < blockOpen)
            throw new WinterForgeFormatException("Expected typed collection syntax '<...>[...]'.");

        string types = value[(typeOpen + 1)..value.IndexOf('>', typeOpen)].Trim();
        string[] typeParts = types.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        bool dict = typeParts.Length == 2;
        if (!dict && typeParts.Length != 1)
            throw new WinterForgeFormatException("Invalid generic parameter count for collection.");

        EmitListStart(typeParts[0], dict ? typeParts[1] : null);

        string items = value[(blockOpen + 1)..blockClose];
        var elements = HRPHelpers.SplitPreserveQuotesAndParentheses(items, ',');
        foreach (var rawElement in elements)
        {
            string e = rawElement.Trim();
            if (string.IsNullOrWhiteSpace(e))
                continue;

            if (dict)
            {
                int split = HRPHelpers.ContainsSequenceOutsideQuotes(e, "=>");
                if (split == -1)
                    throw new WinterForgeFormatException(e, "Dictionary key-value not properly written. Expected 'key => value'");

                string key = ValidateValue(e[..split].Trim(), isBody, currentObjectId);
                string val = ValidateValue(e[(split + 2)..].Trim(), isBody, currentObjectId);
                EmitElement(key, val);
            }
            else
            {
                string v = ValidateValue(e, isBody, currentObjectId);
                EmitElement(v, null);
            }
        }

        EmitNoArgs(OpCode.LIST_END);
        return "#stack()";
    }

    private string ValidateValue(string value, bool isBody, string? currentObjectId)
    {
        string trimmed = NormalizeBuiltins(value.Trim());

        if (TryEnum(trimmed, out object? enumObjValue))
        {
            return enumObjValue?.ToString() ?? "0";
        }

        if (trimmed.StartsWith("\"\"\"\"\"", StringComparison.Ordinal) && trimmed.EndsWith("\"\"\"\"\"", StringComparison.Ordinal) && trimmed.Length >= 10)
            return NormalizeStringLiteral(trimmed);

        if (trimmed.StartsWith('"') && trimmed.EndsWith('"'))
            return NormalizeStringLiteral(trimmed);

        if (HRPHelpers.HasValidGenericFollowedByBracket(trimmed))
            return ParseCollectionValue(trimmed, isBody, currentObjectId);

        if (HRPHelpers.ContainsExpressionOutsideQuotes(trimmed))
        {
            ParseExpression(trimmed, currentObjectId, isBody);
            return "#stack()";
        }

        if (HRPHelpers.ContainsSequenceOutsideQuotes(trimmed, "->") != -1)
        {
            HandleAccessing(trimmed, isBody, currentObjectId);
            return "#stack()";
        }

        if (trimmed is "true" or "false")
        {
            EmitPush(trimmed);
            return "#stack()";
        }

        if (IsMethodCall(trimmed))
        {
            ParseMethodCall(currentObjectId, trimmed, isBody);
            return "#stack()";
        }

        return trimmed;
    }

    private void ParseExpression(string expr, string? currentObjectId, bool isBody)
    {
        if (expr.EndsWith(';'))
            expr = expr[..^1];

        var tokens = ExpressionTokenizer.Tokenize(expr);
        foreach (var token in tokens)
        {
            switch (token.Type)
            {
                case TokenType.Number:
                case TokenType.String:
                case TokenType.Identifier:
                    if (HRPHelpers.ContainsSequenceOutsideQuotes(token.Text, "->") != -1)
                        ParseRhsAccess(token.Text, currentObjectId, isBody);
                    else if (token.Text.EndsWith(')') && token.Text.Contains('('))
                        ParseMethodCall(currentObjectId, token.Text, isBody);
                    else
                        EmitPush(token.Text);
                    break;

                case TokenType.Operator:
                    EmitNoArgs(token.Text switch
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
                    });
                    break;
            }
        }
    }

    private bool TryEnum(string value, out object? enumObjValue)
    {
        enumObjValue = null;
        if (!value.Contains('.') || HRPHelpers.IsValidNumericString(value) || value.Contains('<'))
            return false;

        try
        {
            string[] parts = value.Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                return false;

            string enumType = string.Join('.', parts[..^1]);
            string enumValue = parts[^1];
            Type? e = TypeWorker.FindType(enumType);
            if (e is null || !e.IsEnum)
                return false;

            string[] values = enumValue.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            Enum result = null!;
            for (int i = 0; i < values.Length; i++)
            {
                object parsed = Enum.Parse(e, values[i]);
                result = i == 0 ? (Enum)parsed : (Enum)Enum.ToObject(e, Convert.ToInt32(result, CultureInfo.InvariantCulture) | Convert.ToInt32(parsed, CultureInfo.InvariantCulture));
            }

            enumObjValue = Convert.ChangeType(result, Enum.GetUnderlyingType(e), CultureInfo.InvariantCulture)?.ToString();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsMethodCall(string line)
    {
        if (line.TrimStart().StartsWith('#'))
            return false;
        if (line.TrimStart().StartsWith('_'))
            return false;
        return HRPHelpers.ContainsSequenceOutsideQuotes(line, "(") != -1 && line.EndsWith(')');
    }

    private void PushReferenceLike(string first, string? currentObjectId)
    {
        first = NormalizeBuiltins(first);
        if (first == "this")
        {
            if (currentObjectId is null)
                throw new WinterForgeFormatException("'this' reference outside the bounds of an object");

            EmitPush($"#ref({currentObjectId})");
        }
        else if (first.StartsWith("#ref(", StringComparison.Ordinal) || variables.Contains(first))
        {
            EmitPush(first);
        }
        else if (aliasMap.TryGetValue(first, out int aliasId))
        {
            EmitPush($"#ref({aliasId})");
        }
        else
        {
            EmitPush($"#type({first})");
        }
    }

    private string HeaderText(HumanReadableStatementNode stmt)
    {
        string text = stmt.Text.Trim();
        if (!stmt.IsBlock)
            return text;

        int brace = text.IndexOf('{');
        if (brace == -1)
            return text;
        return text[..brace].Trim();
    }

    private WinterForgeSyntaxException Error(string message, HumanReadableStatementNode stmt, string token)
    {
        int line = 1;
        int col = 1;
        if (stmt.Start > 0 && stmt.Start <= source.Length)
        {
            for (int i = 0; i < stmt.Start; i++)
            {
                if (source[i] == '\n')
                {
                    line++;
                    col = 1;
                }
                else
                {
                    col++;
                }
            }
        }

        return WinterForgeSyntaxException.ForToken(message, source, line, col, token);
    }

    private static string NormalizeBuiltins(string input)
    {
        if (input.StartsWith("_ref(", StringComparison.Ordinal))
            return "#" + input[1..];
        if (input.StartsWith("_stack(", StringComparison.Ordinal))
            return "#" + input[1..];
        if (input.StartsWith("_type(", StringComparison.Ordinal))
            return "#" + input[1..];
        return input;
    }

    private static string NormalizeStringLiteral(string literal)
    {
        if (literal.StartsWith("\"\"\"\"\"", StringComparison.Ordinal) && literal.EndsWith("\"\"\"\"\"", StringComparison.Ordinal) && literal.Length >= 10)
        {
            string innerMulti = literal[5..^5];
            return $"\"{innerMulti}\"";
        }

        if (literal.Length >= 2 && literal.StartsWith('"') && literal.EndsWith('"'))
        {
            string inner = literal[1..^1];
            inner = inner
                .Replace("\\\\", "\\")
                .Replace("\\\"", "\"")
                .Replace("\\n", "\n")
                .Replace("\\t", "\t");
            return $"\"{inner}\"";
        }

        return literal;
    }

    private int GetAutoID() => int.MaxValue - 1000 - autoAsIDs++;

    private void EmitDefine(string type, int id, int ctorArgCount)
    {
        EmitNoArgs(OpCode.DEFINE);
        WriteString(type);
        WriteInt(id);
        WriteInt(ctorArgCount);
    }

    private void EmitSet(string field, string value)
    {
        EmitNoArgs(OpCode.SET);
        WriteString(field);
        WriteAny(value);
    }

    private void EmitSetAccess(string field, string value)
    {
        EmitNoArgs(OpCode.SETACCESS);
        WriteString(field);
        WriteAny(value);
    }

    private void EmitPush(string value)
    {
        EmitNoArgs(OpCode.PUSH);
        WritePrefered(value, ValuePrefix.STRING);
    }

    private void EmitElement(string first, string? second)
    {
        EmitNoArgs(OpCode.ELEMENT);
        writer.Write((byte)(second is null ? 1 : 2));
        WriteAny(first);
        if (second is not null)
            WriteAny(second);
    }

    private void EmitListStart(string itemType, string? valueType)
    {
        EmitNoArgs(OpCode.LIST_START);
        WriteString(itemType);
        if (valueType is not null)
            WriteString(valueType);
    }

    private void EmitCall(string methodName, int argCount)
    {
        EmitNoArgs(OpCode.CALL);
        WriteString(methodName);
        WritePrefered(argCount.ToString(CultureInfo.InvariantCulture), ValuePrefix.INT);
    }

    private void EmitReturn(string value)
    {
        EmitNoArgs(OpCode.RET);
        WritePrefered(value, ValuePrefix.INT);
    }

    private void EmitAnonymousSet(string type, string field, string value)
    {
        EmitNoArgs(OpCode.ANONYMOUS_SET);
        WriteString(type);
        WriteString(field);
        WriteAny(value);
    }

    private void EmitImport(string path, int id)
    {
        EmitNoArgs(OpCode.IMPORT);
        WriteString(path);
        WriteInt(id);
    }

    private void EmitCallableStart(OpCode opcode, string name, List<(string type, string name)> parameters)
    {
        EmitNoArgs(opcode);
        WriteString(name);
        WriteInt(parameters.Count);
        foreach (var p in parameters)
        {
            WriteString(p.type);
            WriteString(p.name);
        }
    }

    private void EmitVarDefault(string value)
    {
        EmitNoArgs(OpCode.VAR_DEFAULT_VALUE);
        WriteAny(value);
    }

    private void EmitSingleString(OpCode opcode, string value)
    {
        EmitNoArgs(opcode);
        WriteString(value);
    }

    private void EmitInt(OpCode opcode, int value)
    {
        EmitNoArgs(opcode);
        WriteInt(value);
    }

    private void EmitLabel(string name) => EmitSingleString(OpCode.LABEL, name);
    private void EmitJump(string label) => EmitSingleString(OpCode.JUMP, label);

    private void EmitNoArgs(OpCode opcode) => writer.Write(opcodeMap[opcode]);

    private void WriteString(string str)
    {
        writer.Write((byte)ValuePrefix.STRING);
        writer.Write(str.Length);
        writer.Write(Encoding.UTF8.GetBytes(str));
    }

    private void WriteInt(int value)
    {
        writer.Write((byte)ValuePrefix.INT);
        writer.Write(value);
    }

    private void WritePrefered(object value, ValuePrefix prefered)
    {
        switch (prefered)
        {
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

            case ValuePrefix.STRING:
            default:
                if (value is string raw)
                {
                    raw = NormalizeBuiltins(raw);
                    if (raw.StartsWith("#ref(") && raw.EndsWith(')'))
                    {
                        writer.Write((byte)ValuePrefix.REF);
                        writer.Write(int.Parse(raw[5..^1], CultureInfo.InvariantCulture));
                    }
                    else if (raw.StartsWith("#stack(") && raw.EndsWith(')'))
                    {
                        writer.Write((byte)ValuePrefix.STACK);
                    }
                    else if (raw == "default")
                    {
                        writer.Write((byte)ValuePrefix.DEFAULT);
                    }
                    else if (raw == "null")
                    {
                        writer.Write((byte)ValuePrefix.NULL);
                    }
                    else
                    {
                        WriteString(raw);
                    }
                }
                else
                {
                    WriteString(value.ToString() ?? string.Empty);
                }
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
                result = (T)Convert.ChangeType(s, typeof(T), CultureInfo.InvariantCulture);
                return true;
            }

            result = (T)Convert.ChangeType(input, typeof(T), CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            result = default!;
            return false;
        }
    }

    private void WriteAny(string raw)
    {
        string v = NormalizeBuiltins(raw.Trim());
        if (v.StartsWith('"') && v.EndsWith('"') && v.Length >= 2)
        {
            WriteString(v[1..^1]);
            return;
        }

        if (v.StartsWith("#ref(") && v.EndsWith(')'))
        {
            writer.Write((byte)ValuePrefix.REF);
            writer.Write(int.Parse(v[5..^1], CultureInfo.InvariantCulture));
            return;
        }

        if (v.StartsWith("#stack(") && v.EndsWith(')'))
        {
            writer.Write((byte)ValuePrefix.STACK);
            return;
        }

        if (v == "default")
        {
            writer.Write((byte)ValuePrefix.DEFAULT);
            return;
        }

        if (v == "null")
        {
            writer.Write((byte)ValuePrefix.NULL);
            return;
        }

        if (bool.TryParse(v, out bool boolVal))
        {
            writer.Write((byte)ValuePrefix.BOOL);
            writer.Write(boolVal);
            return;
        }

        if (byte.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte b))
        {
            writer.Write((byte)ValuePrefix.BYTE);
            writer.Write(b);
            return;
        }

        if (sbyte.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out sbyte sb))
        {
            writer.Write((byte)ValuePrefix.SBYTE);
            writer.Write(sb);
            return;
        }

        if (short.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out short sh))
        {
            writer.Write((byte)ValuePrefix.SHORT);
            writer.Write(sh);
            return;
        }

        if (ushort.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort ush))
        {
            writer.Write((byte)ValuePrefix.USHORT);
            writer.Write(ush);
            return;
        }

        if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
        {
            writer.Write((byte)ValuePrefix.INT);
            writer.Write(i);
            return;
        }

        if (uint.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint ui))
        {
            writer.Write((byte)ValuePrefix.UINT);
            writer.Write(ui);
            return;
        }

        if (long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
        {
            writer.Write((byte)ValuePrefix.LONG);
            writer.Write(l);
            return;
        }

        if (ulong.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong ul))
        {
            writer.Write((byte)ValuePrefix.ULONG);
            writer.Write(ul);
            return;
        }

        if (float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
        {
            writer.Write((byte)ValuePrefix.FLOAT);
            writer.Write(f);
            return;
        }

        if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
        {
            writer.Write((byte)ValuePrefix.DOUBLE);
            writer.Write(d);
            return;
        }

        if (decimal.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal m))
        {
            writer.Write((byte)ValuePrefix.DECIMAL);
            writer.Write(m);
            return;
        }

        if (char.TryParse(v, out char c))
        {
            writer.Write((byte)ValuePrefix.CHAR);
            writer.Write(c);
            return;
        }

        WriteString(v);
    }
}
