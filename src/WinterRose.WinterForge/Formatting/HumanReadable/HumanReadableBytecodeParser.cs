using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using WinterRose.Reflection;
using WinterRose.WinterForgeSerializing.Compiling;
using WinterRose.WinterForgeSerializing.Expressions;
using WinterRose.WinterForgeSerializing.Instructions;

namespace WinterRose.WinterForgeSerializing.Formatting;

/// <summary>
/// New human-readable parser pipeline that performs lexing + AST parsing and emits optimized bytecode.
/// </summary>
public sealed class HumanReadableBytecodeParser
{
    /// <summary>
    /// Parses human-readable WinterForge syntax into optimized bytecode.
    /// </summary>
    public void Parse(Stream input, Stream output, bool allowCustomCompilers = true)
    {
        if (input is null) throw new ArgumentNullException(nameof(input));
        if (output is null) throw new ArgumentNullException(nameof(output));
        _ = allowCustomCompilers;

        string source;
        using (var reader = new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true))
            source = reader.ReadToEnd();

        IReadOnlyList<HumanReadableToken> tokens = HumanReadableLexer.Tokenize(source);
        HumanReadableProgramNode ast = HumanReadableAstParser.Parse(source, tokens);

        HumanReadableAstBytecodeCompiler.Compile(ast, source, output);
    }

    /// <summary>
    /// Builds and returns a text-tree visualization of the AST for the given source stream.
    /// </summary>
    public string VisualizeAst(Stream input)
    {
        if (input is null) throw new ArgumentNullException(nameof(input));

        using var reader = new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        string source = reader.ReadToEnd();
        return VisualizeAst(source);
    }

    /// <summary>
    /// Builds and returns a text-tree visualization of the AST for the given source text.
    /// </summary>
    public string VisualizeAst(string source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        IReadOnlyList<HumanReadableToken> tokens = HumanReadableLexer.Tokenize(source);
        HumanReadableProgramNode ast = HumanReadableAstParser.Parse(source, tokens);
        return HumanReadableAstVisualizer.Visualize(ast, source);
    }
}

internal sealed record HumanReadableToken(
    HumanReadableTokenKind Kind,
    string Text,
    int Line,
    int Column,
    int Start,
    int Length);

internal enum HumanReadableTokenKind
{
    Identifier,
    Number,
    String,
    Comment,
    Operator,
    Symbol,
    NewLine,
    EndOfFile
}

internal static class HumanReadableLexer
{
    public static IReadOnlyList<HumanReadableToken> Tokenize(string source)
    {
        List<HumanReadableToken> tokens = [];
        int i = 0;
        int line = 1;
        int column = 1;

        while (i < source.Length)
        {
            char c = source[i];

            if (c == '\r' || c == '\n')
            {
                int start = i;
                int startColumn = column;
                if (c == '\r' && i + 1 < source.Length && source[i + 1] == '\n')
                    i++;

                i++;
                tokens.Add(new HumanReadableToken(HumanReadableTokenKind.NewLine, "\\n", line, startColumn, start, i - start));
                line++;
                column = 1;
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                i++;
                column++;
                continue;
            }

            if (c == '/' && i + 1 < source.Length && source[i + 1] == '/')
            {
                int start = i;
                int startColumn = column;
                i += 2;
                column += 2;
                while (i < source.Length && source[i] is not '\r' and not '\n')
                {
                    i++;
                    column++;
                }

                string text = source[start..i];
                tokens.Add(new HumanReadableToken(HumanReadableTokenKind.Comment, text, line, startColumn, start, text.Length));
                continue;
            }

            if (c == '"')
            {
                tokens.Add(ReadStringToken(source, ref i, ref line, ref column));
                continue;
            }

            if (TryReadOperator(source, ref i, ref column, line, out HumanReadableToken opToken))
            {
                tokens.Add(opToken);
                continue;
            }

            if (IsSymbol(c))
            {
                int start = i;
                int startColumn = column;
                i++;
                column++;
                tokens.Add(new HumanReadableToken(HumanReadableTokenKind.Symbol, c.ToString(), line, startColumn, start, 1));
                continue;
            }

            if (char.IsDigit(c))
            {
                int start = i;
                int startColumn = column;
                i++;
                column++;
                while (i < source.Length && (char.IsDigit(source[i]) || source[i] == '.'))
                {
                    i++;
                    column++;
                }

                string text = source[start..i];
                tokens.Add(new HumanReadableToken(HumanReadableTokenKind.Number, text, line, startColumn, start, text.Length));
                continue;
            }

            if (IsIdentifierStart(c))
            {
                int start = i;
                int startColumn = column;
                i++;
                column++;
                while (i < source.Length)
                {
                    // Do not absorb '-' when it starts an access operator (->)
                    // Otherwise identifiers like "System.Math->Abs" would tokenize as "System.Math-" + ">".
                    if (source[i] == '-' && i + 1 < source.Length && source[i + 1] == '>')
                        break;

                    if (!IsIdentifierPart(source[i]))
                        break;

                    i++;
                    column++;
                }

                string text = source[start..i];
                tokens.Add(new HumanReadableToken(HumanReadableTokenKind.Identifier, text, line, startColumn, start, text.Length));
                continue;
            }

            throw HumanReadableSyntaxException.ForToken(
                "Unexpected token.",
                source,
                line,
                column,
                c.ToString());
        }

        tokens.Add(new HumanReadableToken(HumanReadableTokenKind.EndOfFile, string.Empty, line, column, source.Length, 0));
        return tokens;
    }

    private static HumanReadableToken ReadStringToken(string source, ref int i, ref int line, ref int column)
    {
        int start = i;
        int startLine = line;
        int startColumn = column;

        int quoteCount = 0;
        while (i < source.Length && source[i] == '"' && quoteCount < 5)
        {
            i++;
            column++;
            quoteCount++;
        }

        if (quoteCount is not (1 or 5))
            throw HumanReadableSyntaxException.ForToken("Invalid quote count. Use 1 or 5 quotes.", source, startLine, startColumn, source[start..Math.Min(source.Length, start + quoteCount)]);

        bool multiline = quoteCount == 5;
        bool escaped = false;
        int quoteRun = 0;

        while (i < source.Length)
        {
            char c = source[i];

            if (!multiline && (c == '\r' || c == '\n'))
                throw HumanReadableSyntaxException.ForToken("Unterminated single-line string.", source, startLine, startColumn, source[start..Math.Min(source.Length, start + 1)]);

            i++;
            if (c == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }

            if (escaped)
            {
                escaped = false;
                quoteRun = 0;
                continue;
            }

            if (c == '\\')
            {
                escaped = true;
                quoteRun = 0;
                continue;
            }

            if (c == '"')
            {
                quoteRun++;
                if (!multiline && quoteRun >= 1)
                {
                    string text = source[start..i];
                    return new HumanReadableToken(HumanReadableTokenKind.String, text, startLine, startColumn, start, text.Length);
                }

                if (multiline && quoteRun >= 5)
                {
                    string text = source[start..i];
                    return new HumanReadableToken(HumanReadableTokenKind.String, text, startLine, startColumn, start, text.Length);
                }

                continue;
            }

            quoteRun = 0;
        }

        throw HumanReadableSyntaxException.ForToken("Unterminated string.", source, startLine, startColumn, source[start..Math.Min(source.Length, start + 1)]);
    }

    private static bool TryReadOperator(string source, ref int i, ref int column, int line, out HumanReadableToken token)
    {
        static bool Is2(string v) => v is "->" or "=>" or "==" or "!=" or ">=" or "<=" or "&&" or "||" or "^|";
        static bool Is1(char c) => c is '=' or '+' or '-' or '*' or '/' or '%' or '^' or '!' or '>' or '<' or ':';

        token = default!;

        if (i + 1 < source.Length)
        {
            string two = source.Substring(i, 2);
            if (Is2(two))
            {
                token = new HumanReadableToken(HumanReadableTokenKind.Operator, two, line, column, i, 2);
                i += 2;
                column += 2;
                return true;
            }
        }

        char one = source[i];
        if (!Is1(one))
            return false;

        token = new HumanReadableToken(HumanReadableTokenKind.Operator, one.ToString(), line, column, i, 1);
        i++;
        column++;
        return true;
    }

    private static bool IsSymbol(char c) => c is '{' or '}' or '(' or ')' or '[' or ']' or ',' or ';';

    private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c is '_' or '#' or '$';

    private static bool IsIdentifierPart(char c)
        => char.IsLetterOrDigit(c) || c is '_' or '#' or '.' or '-' or '$';
}

internal abstract record HumanReadableAstNode(int Start, int End, string Text);

internal sealed record HumanReadableProgramNode(IReadOnlyList<HumanReadableStatementNode> Statements, int Start, int End, string Text)
    : HumanReadableAstNode(Start, End, Text);

internal sealed record HumanReadableStatementNode(
    IReadOnlyList<HumanReadableStatementNode> Children,
    bool IsBlock,
    int Start,
    int End,
    string Text)
    : HumanReadableAstNode(Start, End, Text);

internal static class HumanReadableAstParser
{
    public static HumanReadableProgramNode Parse(string source, IReadOnlyList<HumanReadableToken> tokens)
    {
        TokenReader reader = new(source, tokens);
        var statements = ParseStatements(reader, stopOnCloseBrace: false);
        int start = statements.Count == 0 ? 0 : statements[0].Start;
        int end = statements.Count == 0 ? 0 : statements[^1].End;
        string text = statements.Count == 0 ? string.Empty : source[start..end];
        return new HumanReadableProgramNode(statements, start, end, text);
    }

    private static List<HumanReadableStatementNode> ParseStatements(TokenReader reader, bool stopOnCloseBrace)
    {
        List<HumanReadableStatementNode> statements = [];

        while (!reader.IsEnd)
        {
            reader.SkipTrivia();
            if (reader.IsEnd)
                break;

            if (reader.Current.Kind == HumanReadableTokenKind.Symbol && reader.Current.Text == "}")
            {
                if (!stopOnCloseBrace)
                    throw reader.Error("Unexpected closing brace.", reader.Current);

                reader.Advance();
                return statements;
            }

            statements.Add(ParseStatement(reader));
        }

        if (stopOnCloseBrace)
            throw reader.Error(
                "Missing closing brace for block.",
                reader.PreviousOrCurrent(),
                "Did you mean to place '}' before end of file?");

        return statements;
    }

    private static HumanReadableStatementNode ParseStatement(TokenReader reader)
    {
        HumanReadableToken startToken = reader.Current;

        Stack<HumanReadableToken> parenStack = new();
        Stack<HumanReadableToken> bracketStack = new();
        Stack<HumanReadableToken> angleStack = new();

        bool sawBlockOpen = false;
        while (!reader.IsEnd)
        {
            HumanReadableToken token = reader.Current;

            if (token.Kind == HumanReadableTokenKind.NewLine && parenStack.Count == 0 && bracketStack.Count == 0 && angleStack.Count == 0)
            {
                reader.Advance();
                break;
            }

            if (token.Kind == HumanReadableTokenKind.Symbol)
            {
                if (token.Text == ";" && parenStack.Count == 0 && bracketStack.Count == 0 && angleStack.Count == 0)
                {
                    reader.Advance();
                    break;
                }

                if (token.Text == "{" && parenStack.Count == 0 && bracketStack.Count == 0 && angleStack.Count == 0)
                {
                    sawBlockOpen = true;
                    reader.Advance();
                    break;
                }
            }

            if (token.Text == "(")
            {
                parenStack.Push(token);
            }
            else if (token.Text == ")")
            {
                if (parenStack.Count == 0)
                {
                    var diagnostic = BuildUnexpectedClosingReason(reader, token, parenStack.Count, bracketStack.Count, angleStack.Count);
                    throw reader.Error(diagnostic.reason, token, diagnostic.suggestion);
                }
                parenStack.Pop();
            }
            else if (token.Text == "[")
            {
                bracketStack.Push(token);
            }
            else if (token.Text == "]")
            {
                if (bracketStack.Count == 0)
                {
                    var diagnostic = BuildUnexpectedClosingReason(reader, token, parenStack.Count, bracketStack.Count, angleStack.Count);
                    throw reader.Error(diagnostic.reason, token, diagnostic.suggestion);
                }
                bracketStack.Pop();
            }
            else if (token.Text == "<")
            {
                angleStack.Push(token);
            }
            else if (token.Text == ">")
            {
                if (angleStack.Count == 0)
                {
                    var diagnostic = BuildUnexpectedClosingReason(reader, token, parenStack.Count, bracketStack.Count, angleStack.Count);
                    throw reader.Error(diagnostic.reason, token, diagnostic.suggestion);
                }
                angleStack.Pop();
            }

            if (parenStack.Count < 0 || bracketStack.Count < 0 || angleStack.Count < 0)
            {
                var diagnostic = BuildUnexpectedClosingReason(reader, token, parenStack.Count, bracketStack.Count, angleStack.Count);
                throw reader.Error(diagnostic.reason, token, diagnostic.suggestion);
            }

            reader.Advance();
        }

        if (parenStack.Count != 0 || bracketStack.Count != 0 || angleStack.Count != 0)
        {
            HumanReadableToken openingToken;
            string expectedClosing;
            string openingName;

            if (bracketStack.Count > 0)
            {
                openingToken = bracketStack.Peek();
                expectedClosing = "]";
                openingName = "[";
            }
            else if (parenStack.Count > 0)
            {
                openingToken = parenStack.Peek();
                expectedClosing = ")";
                openingName = "(";
            }
            else
            {
                openingToken = angleStack.Peek();
                expectedClosing = ">";
                openingName = "<";
            }

            string reason = $"Unclosed grouping token. '{openingName}' was opened here but no matching '{expectedClosing}' was found before end of statement.";
            string suggestion = $"Did you mean to place '{expectedClosing}' before the end of this statement?";
            throw reader.Error(reason, openingToken, suggestion);
        }

        if (!sawBlockOpen)
        {
            int end = reader.PreviousOrCurrent().Start + reader.PreviousOrCurrent().Length;
            string text = SafeSlice(reader.Source, startToken.Start, end);
            return new HumanReadableStatementNode([], false, startToken.Start, end, text);
        }

        List<HumanReadableStatementNode> children = ParseStatements(reader, stopOnCloseBrace: true);
        int blockEnd = reader.PreviousOrCurrent().Start + reader.PreviousOrCurrent().Length;
        string blockText = SafeSlice(reader.Source, startToken.Start, blockEnd);
        return new HumanReadableStatementNode(children, true, startToken.Start, blockEnd, blockText);

        static (string reason, string? suggestion) BuildUnexpectedClosingReason(TokenReader reader, HumanReadableToken token, int parenDepth, int bracketDepth, int angleDepth)
        {
            string previous = reader.Previous().Text;
            string next = reader.Next().Text;
            string baseReason = $"Unexpected closing token while parsing statement. Token='{token.Text}', previous='{previous}', next='{next}', depth(parens={parenDepth}, brackets={bracketDepth}, angles={angleDepth}).";

            if (token.Text == ">" && previous.EndsWith("-", StringComparison.Ordinal))
                return (baseReason + " Hint: this often means '->' was split incorrectly.", "Did you mean to use '->' for access, e.g. Type->Member?");

            if (token.Text is ")" or "]" or ">")
                return (baseReason + " Hint: check for a missing opening token earlier on the same line.", $"Did you mean to add a matching opening token before '{token.Text}'?");

            return (baseReason, null);
        }
    }

    private static string SafeSlice(string source, int start, int end)
    {
        if (start < 0 || end < start || end > source.Length)
            return string.Empty;
        return source[start..end];
    }

    private sealed class TokenReader
    {
        private readonly IReadOnlyList<HumanReadableToken> tokens;
        private int index;

        public TokenReader(string source, IReadOnlyList<HumanReadableToken> tokens)
        {
            Source = source;
            this.tokens = tokens;
            index = 0;
        }

        public string Source { get; }
        public HumanReadableToken Current => tokens[Math.Min(index, tokens.Count - 1)];
        public bool IsEnd => Current.Kind == HumanReadableTokenKind.EndOfFile;

        public void Advance()
        {
            if (index < tokens.Count - 1)
                index++;
        }

        public HumanReadableToken PreviousOrCurrent() => tokens[Math.Max(0, Math.Min(index - 1, tokens.Count - 1))];

        public HumanReadableToken Previous() => tokens[Math.Max(0, Math.Min(index - 1, tokens.Count - 1))];

        public HumanReadableToken Next() => tokens[Math.Max(0, Math.Min(index + 1, tokens.Count - 1))];

        public void SkipTrivia()
        {
            while (!IsEnd && (Current.Kind == HumanReadableTokenKind.NewLine || Current.Kind == HumanReadableTokenKind.Comment))
                Advance();
        }

        public HumanReadableSyntaxException Error(string message, HumanReadableToken token, string? suggestion = null)
            => HumanReadableSyntaxException.ForToken(message, Source, token.Line, token.Column, token.Text, suggestion);
    }
}

internal static class HumanReadableAstBytecodeCompiler
{
    private static readonly Dictionary<OpCode, byte> opcodeMap = Enum
        .GetValues<OpCode>()
        .ToDictionary(op => op, op => (byte)op);

    public static void Compile(HumanReadableProgramNode program, string source, Stream output)
    {
        using BinaryWriter writer = new(output, Encoding.UTF8, leaveOpen: true);
        Compiler compiler = new(source, writer);
        compiler.CompileProgram(program);
        writer.Flush();
    }

    private sealed class Compiler
    {
        private readonly string source;
        private readonly BinaryWriter writer;
        private readonly Dictionary<string, int> aliasMap = [];
        private readonly HashSet<string> variables = [];
        private readonly List<(string start, string end)> flowLabels = [];
        private int autoAsIDs;

        public Compiler(string source, BinaryWriter writer)
        {
            this.source = source;
            this.writer = writer;
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

            bool hasColon = HRPHelpers.ContainsSequenceOutsideQuotes(line, ":") != -1;
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

            for (int i = 0; i < stmt.Children.Count; i++)
            {
                var child = stmt.Children[i];
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

        private HumanReadableSyntaxException Error(string message, HumanReadableStatementNode stmt, string token)
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

            return HumanReadableSyntaxException.ForToken(message, source, line, col, token);
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
}

internal static class HumanReadableAstVisualizer
{
    private sealed class VisualDetail
    {
        public VisualDetail(string text)
        {
            Text = text;
        }

        public string Text { get; }
        public List<VisualDetail> Children { get; } = [];
    }

    private sealed class ExprNode
    {
        public ExprNode(string text, bool isOperator)
        {
            Text = text;
            IsOperator = isOperator;
        }

        public string Text { get; }
        public bool IsOperator { get; }
        public List<ExprNode> Children { get; } = [];
    }

    public static string Visualize(HumanReadableProgramNode program, string source)
    {
        StringBuilder sb = new();
        sb.AppendLine("Program");

        for (int i = 0; i < program.Statements.Count; i++)
        {
            bool isLast = i == program.Statements.Count - 1;
            AppendStatement(sb, program.Statements[i], source, prefix: string.Empty, isLast);
        }

        return sb.ToString();
    }

    private static void AppendStatement(StringBuilder sb, HumanReadableStatementNode node, string source, string prefix, bool isLast)
    {
        string branch = isLast ? "└─" : "├─";
        string childPrefix = prefix + (isLast ? "  " : "│ ");

        (int line, int column) = GetLineAndColumn(source, node.Start);
        string type = node.IsBlock ? "BlockStatement" : "Statement";
        string preview = NormalizePreview(node.Text);
        sb.Append(prefix)
            .Append(branch)
            .Append(type)
            .Append(" [")
            .Append(line)
            .Append(':')
            .Append(column)
            .Append("] ")
            .AppendLine(preview);

        AppendStatementDetails(sb, node, childPrefix);

        for (int i = 0; i < node.Children.Count; i++)
        {
            bool childLast = i == node.Children.Count - 1;
            AppendStatement(sb, node.Children[i], source, childPrefix, childLast);
        }
    }

    private static void AppendStatementDetails(StringBuilder sb, HumanReadableStatementNode node, string prefix)
    {
        string raw = node.Text.Replace("\r", "").Trim();
        string header = node.IsBlock
            ? raw[..Math.Max(0, raw.IndexOf('{'))].Trim()
            : raw.TrimEnd(';').Trim();

        if (string.IsNullOrWhiteSpace(header))
            return;

        var details = AnalyzeStatement(header);
        AppendDetailsTree(sb, details, prefix, node.Children.Count == 0);
    }

    private static void AppendDetailsTree(StringBuilder sb, List<VisualDetail> details, string prefix, bool statementHasNoChildren)
    {
        for (int i = 0; i < details.Count; i++)
        {
            bool isLast = i == details.Count - 1;
            AppendDetail(sb, details[i], prefix, isLast && statementHasNoChildren);
        }
    }

    private static void AppendDetail(StringBuilder sb, VisualDetail detail, string prefix, bool isLast)
    {
        string branch = isLast ? "└─" : "├─";
        string childPrefix = prefix + (isLast ? "  " : "│ ");

        sb.Append(prefix).Append(branch).AppendLine(detail.Text);

        for (int i = 0; i < detail.Children.Count; i++)
        {
            bool childLast = i == detail.Children.Count - 1;
            AppendDetail(sb, detail.Children[i], childPrefix, childLast);
        }
    }

    private static List<VisualDetail> AnalyzeStatement(string header)
    {
        List<VisualDetail> details = [];

        if (header.StartsWith("#import", StringComparison.OrdinalIgnoreCase))
        {
            details.Add(new VisualDetail("kind: import"));
            details.Add(new VisualDetail($"source: {header[7..].Trim()}"));
            return details;
        }

        if (header.StartsWith("#container", StringComparison.Ordinal))
        {
            details.Add(new VisualDetail("kind: container"));
            details.Add(new VisualDetail($"name: {header["#container".Length..].Trim()}"));
            return details;
        }

        if (header.StartsWith("#template", StringComparison.Ordinal))
        {
            details.Add(new VisualDetail("kind: template"));
            details.Add(new VisualDetail($"signature: {header["#template".Length..].Trim()}"));
            return details;
        }

        if (header.StartsWith("if ", StringComparison.Ordinal))
        {
            details.Add(new VisualDetail("kind: if"));
            details.Add(new VisualDetail($"condition: {header[2..].Trim()}"));
            return details;
        }

        if (header.StartsWith("else if ", StringComparison.Ordinal))
        {
            details.Add(new VisualDetail("kind: else-if"));
            details.Add(new VisualDetail($"condition: {header[7..].Trim()}"));
            return details;
        }

        if (header == "else" || header.StartsWith("else ", StringComparison.Ordinal))
        {
            details.Add(new VisualDetail("kind: else"));
            return details;
        }

        if (header.StartsWith("while", StringComparison.Ordinal))
        {
            details.Add(new VisualDetail("kind: while"));
            details.Add(new VisualDetail($"condition: {header["while".Length..].Trim()}"));
            return details;
        }

        if (header.StartsWith("for ", StringComparison.Ordinal))
        {
            details.Add(new VisualDetail("kind: for"));
            string[] parts = HRPHelpers.SplitForLoopContent(header["for".Length..].Trim());
            if (parts.Length >= 3)
            {
                details.Add(new VisualDetail($"initializer: {parts[0].Trim()}"));
                details.Add(new VisualDetail($"condition: {parts[1].Trim()}"));
                details.Add(new VisualDetail($"iterator: {parts[2].Trim()}"));
            }
            return details;
        }

        if (header.StartsWith("return", StringComparison.Ordinal))
        {
            details.Add(new VisualDetail("kind: return"));
            details.Add(new VisualDetail($"value: {header[6..].Trim()}"));
            return details;
        }

        if (header.EndsWith(':') && !header.Contains('='))
        {
            details.Add(new VisualDetail("kind: label"));
            details.Add(new VisualDetail($"name: {header[..^1].Trim()}"));
            return details;
        }

        if (header.StartsWith("goto ", StringComparison.Ordinal))
        {
            details.Add(new VisualDetail("kind: goto"));
            details.Add(new VisualDetail($"label: {header["goto".Length..].Trim()}"));
            return details;
        }

        int objectColon = HRPHelpers.ContainsSequenceOutsideQuotes(header, ":");
        if (objectColon != -1 && objectColon > 0)
        {
            string left = header[..objectColon].Trim();
            string right = header[(objectColon + 1)..].Trim();
            if (!left.Contains('=') && !left.StartsWith("var ", StringComparison.Ordinal) && !left.StartsWith("global ", StringComparison.Ordinal))
            {
                details.Add(new VisualDetail("kind: object-definition"));
                details.Add(new VisualDetail($"type/signature: {left}"));
                details.Add(new VisualDetail($"id/alias: {right}"));
                return details;
            }
        }

        int eq = HRPHelpers.ContainsSequenceOutsideQuotes(header, "=");
        if (eq != -1)
        {
            string lhs = header[..eq].Trim();
            string rhs = header[(eq + 1)..].Trim();

            if (header.StartsWith("var ", StringComparison.Ordinal))
                details.Add(new VisualDetail("kind: variable-definition"));
            else if (header.StartsWith("global ", StringComparison.Ordinal))
                details.Add(new VisualDetail("kind: global-variable-definition"));
            else if (lhs.Contains("->", StringComparison.Ordinal))
                details.Add(new VisualDetail("kind: access-assignment"));
            else
                details.Add(new VisualDetail("kind: assignment"));

            details.Add(new VisualDetail($"lhs: {lhs}"));
            details.Add(new VisualDetail($"rhs: {rhs}"));

            if (HRPHelpers.HasValidGenericFollowedByBracket(rhs))
            {
                VisualDetail collectionRoot = new("rhs-kind: typed-collection");
                int lt = rhs.IndexOf('<');
                int gt = rhs.IndexOf('>');
                if (lt >= 0 && gt > lt)
                {
                    string generic = rhs[(lt + 1)..gt].Trim();
                    string[] genParts = generic.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    if (genParts.Length == 1)
                    {
                        collectionRoot.Children.Add(new VisualDetail("collection: list/array"));
                        collectionRoot.Children.Add(new VisualDetail($"element-type: {genParts[0]}"));
                    }
                    else if (genParts.Length == 2)
                    {
                        collectionRoot.Children.Add(new VisualDetail("collection: dictionary"));
                        collectionRoot.Children.Add(new VisualDetail($"key-type: {genParts[0]}"));
                        collectionRoot.Children.Add(new VisualDetail($"value-type: {genParts[1]}"));
                    }
                }

                int open = rhs.IndexOf('[');
                int close = rhs.LastIndexOf(']');
                if (open >= 0 && close > open)
                {
                    string items = rhs[(open + 1)..close];
                    var elementTokens = HRPHelpers.SplitPreserveQuotesAndParentheses(items, ',')
                        .Select(e => e.Trim())
                        .Where(e => !string.IsNullOrWhiteSpace(e))
                        .ToList();

                    VisualDetail elementsNode = new($"elements ({elementTokens.Count})");
                    bool isDict = rhs.Contains(",", StringComparison.Ordinal) && rhs.IndexOf('<') < rhs.IndexOf('>') && rhs[rhs.IndexOf('<')..rhs.IndexOf('>')].Contains(',');
                    foreach (string element in elementTokens)
                    {
                        if (isDict)
                        {
                            int split = HRPHelpers.ContainsSequenceOutsideQuotes(element, "=>");
                            if (split != -1)
                            {
                                VisualDetail kv = new("entry");
                                kv.Children.Add(new VisualDetail($"key: {element[..split].Trim()}"));
                                kv.Children.Add(new VisualDetail($"value: {element[(split + 2)..].Trim()}"));
                                elementsNode.Children.Add(kv);
                                continue;
                            }
                        }

                        elementsNode.Children.Add(new VisualDetail($"item: {element}"));
                    }

                    collectionRoot.Children.Add(elementsNode);
                }

                details.Add(collectionRoot);
            }
            else if (HRPHelpers.ContainsExpressionOutsideQuotes(rhs))
            {
                ExprNode? expr = TryBuildExpressionTree(rhs);
                if (expr is not null)
                {
                    VisualDetail exprRoot = new("rhs-expression");
                    exprRoot.Children.Add(ToExpressionVisual(expr));
                    details.Add(exprRoot);
                }
            }

            return details;
        }

        if (HRPHelpers.ContainsSequenceOutsideQuotes(header, "(") != -1 && header.EndsWith(')'))
        {
            details.Add(new VisualDetail("kind: method-call"));
            details.Add(new VisualDetail($"call: {header}"));
            return details;
        }

        details.Add(new VisualDetail("kind: unknown"));
        details.Add(new VisualDetail($"raw: {header}"));
        return details;
    }

    private static ExprNode? TryBuildExpressionTree(string expr)
    {
        try
        {
            var tokens = ExpressionTokenizer.Tokenize(expr);
            Stack<ExprNode> stack = new();

            for (int i = 0; i < tokens.Count; i++)
            {
                var t = tokens[i];
                if (t.Type is TokenType.Number or TokenType.String or TokenType.Identifier)
                {
                    stack.Push(new ExprNode(t.Text, isOperator: false));
                    continue;
                }

                if (t.Type == TokenType.Operator)
                {
                    ExprNode op = new(t.Text, isOperator: true);
                    if (t.Text == "!")
                    {
                        if (stack.Count < 1)
                            return null;
                        op.Children.Add(stack.Pop());
                        stack.Push(op);
                        continue;
                    }

                    if (stack.Count < 2)
                        return null;

                    ExprNode right = stack.Pop();
                    ExprNode left = stack.Pop();
                    op.Children.Add(left);
                    op.Children.Add(right);
                    stack.Push(op);
                }
            }

            return stack.Count == 1 ? stack.Pop() : null;
        }
        catch
        {
            return null;
        }
    }

    private static VisualDetail ToExpressionVisual(ExprNode node)
    {
        VisualDetail d = new(node.IsOperator ? $"op: {node.Text}" : $"value: {node.Text}");
        for (int i = 0; i < node.Children.Count; i++)
            d.Children.Add(ToExpressionVisual(node.Children[i]));
        return d;
    }

    private static (int line, int column) GetLineAndColumn(string source, int index)
    {
        int line = 1;
        int column = 1;
        int limit = Math.Min(index, source.Length);

        for (int i = 0; i < limit; i++)
        {
            if (source[i] == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
        }

        return (line, column);
    }

    private static string NormalizePreview(string text)
    {
        string cleaned = text
            .Replace("\r", "")
            .Replace("\n", " ")
            .Trim();

        if (cleaned.Length > 120)
            cleaned = cleaned[..117] + "...";

        return cleaned;
    }
}

internal static class HumanReadableAstSourceEmitter
{
    public static string Emit(HumanReadableProgramNode program)
    {
        if (program.Statements.Count == 0)
            return string.Empty;

        StringBuilder sb = new();
        for (int i = 0; i < program.Statements.Count; i++)
        {
            if (i > 0)
                sb.AppendLine();

            sb.Append(program.Statements[i].Text.Trim());
        }

        return sb.ToString();
    }
}

public sealed class HumanReadableSyntaxException : WinterForgeFormatException
{
    /// <summary>
    /// How many lines before and after the error line should be included in the diagnostic context.
    /// </summary>
    public static int ContextLineRadius { get; set; } = 2;

    public int Line { get; }
    public int Column { get; }
    public string FaultyToken { get; }
    public int ContextStartLine { get; }
    public int ContextEndLine { get; }
    public string SourceContext { get; }
    public string Reason { get; }
    public string? Suggestion { get; }
    public string DiagnosticText { get; }

    private HumanReadableSyntaxException(
        string message,
        int line,
        int column,
        string faultyToken,
        string reason,
        string? suggestion,
        int contextStart,
        int contextEnd,
        string context,
        string diagnosticText)
        : base(diagnosticText)
    {
        Line = line;
        Column = column;
        FaultyToken = faultyToken;
        Reason = reason;
        Suggestion = suggestion;
        ContextStartLine = contextStart;
        ContextEndLine = contextEnd;
        SourceContext = context;
        DiagnosticText = diagnosticText;
    }

    internal static HumanReadableSyntaxException ForToken(string reason, string source, int line, int column, string token, string? suggestion = null)
    {
        token ??= string.Empty;
        string[] lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        int safeLine = Math.Clamp(line, 1, Math.Max(1, lines.Length));
        int safeColumn = Math.Max(1, column);
        int radius = Math.Max(0, ContextLineRadius);
        int start = Math.Max(1, safeLine - radius);
        int end = Math.Min(lines.Length, safeLine + radius);

        string lineText = lines.Length == 0 ? string.Empty : lines[safeLine - 1];
        int tokenWidth = Math.Max(1, GetDisplayTokenWidth(token));
        int caretCol = Math.Clamp(safeColumn, 1, Math.Max(1, lineText.Length + 1));

        string context = BuildContext(lines, start, end, safeLine, caretCol, tokenWidth);
        string diagnostic = BuildDiagnostic("WF0001", "Invalid Syntax", reason, token, safeLine, safeColumn, context, suggestion);

        return new HumanReadableSyntaxException(
            message: $"Syntax error at line {safeLine}, column {safeColumn}",
            line: safeLine,
            column: safeColumn,
            faultyToken: token,
            reason: reason,
            suggestion: suggestion,
            contextStart: start,
            contextEnd: end,
            context: context,
            diagnosticText: diagnostic);
    }

    private static string BuildDiagnostic(string errorCode, string shortReason, string reason, string token, int line, int column, string context, string? suggestion)
    {
        string shownToken = string.IsNullOrWhiteSpace(token) ? "<unknown>" : token.Replace("\n", "\\n").Replace("\r", "\\r");
        StringBuilder sb = new();
        sb.AppendLine($"error[{errorCode}]: {shortReason}");
        sb.AppendLine($"  --> line {line}, column {column}");
        sb.AppendLine("   |");
        sb.Append(context);
        sb.AppendLine("   |");
        sb.AppendLine($"   = token: `{shownToken}`");
        sb.AppendLine($"   = reason: {reason}");
        if (!string.IsNullOrWhiteSpace(suggestion))
            sb.AppendLine($"   = help: {suggestion}");
        return sb.ToString().TrimEnd();
    }

    private static string BuildContext(string[] lines, int start, int end, int errorLine, int caretCol, int tokenWidth)
    {
        int lineDigits = Math.Max(2, end.ToString(CultureInfo.InvariantCulture).Length);
        StringBuilder sb = new();

        for (int l = start; l <= end; l++)
        {
            string text = l - 1 < lines.Length ? lines[l - 1] : string.Empty;
            sb.Append(' ')
              .Append(l.ToString(CultureInfo.InvariantCulture).PadLeft(lineDigits))
              .Append(" | ")
              .AppendLine(text);

            if (l == errorLine)
            {
                sb.Append(' ')
                  .Append(new string(' ', lineDigits))
                  .Append(" | ")
                  .Append(new string(' ', Math.Max(0, caretCol - 1)))
                  .Append('^');

                if (tokenWidth > 1)
                    sb.Append(new string('~', tokenWidth - 1));

                sb.AppendLine();
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static int GetDisplayTokenWidth(string token)
    {
        if (string.IsNullOrEmpty(token))
            return 1;

        int width = 0;
        foreach (char c in token)
        {
            if (c is '\r' or '\n')
                break;
            width++;
        }

        return Math.Max(1, width);
    }
}
