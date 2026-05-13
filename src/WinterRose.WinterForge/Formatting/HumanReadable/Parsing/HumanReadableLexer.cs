namespace WinterRose.WinterForgeSerializing.Formatting.HumanReadable.Parsing;

using System;
using System.Collections.Generic;
using System.Globalization;

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

            if (c is '\r' or '\n')
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

            if (c == ' ' || c == '\t')
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

            if (c == '#')
            {
                int start = i;
                int startColumn = column;

                i++;
                column++;

                while (i < source.Length && IsIdentifierPart(source[i]))
                {
                    i++;
                    column++;
                }

                string text = source[start..i];
                tokens.Add(new HumanReadableToken(HumanReadableTokenKind.Directive, text, line, startColumn, start, text.Length));
                continue;
            }

            if (c == '"')
            {
                tokens.Add(ReadStringToken(source, ref i, ref line, ref column));
                continue;
            }

            if (TryReadOperatorOrPunctuation(source, ref i, ref column, line, out HumanReadableToken opToken))
            {
                tokens.Add(opToken);
                continue;
            }

            if (char.IsDigit(c))
            {
                int start = i;
                int startColumn = column;
                bool seenDot = false;

                i++;
                column++;

                while (i < source.Length)
                {
                    char next = source[i];
                    if (char.IsDigit(next))
                    {
                        i++;
                        column++;
                        continue;
                    }

                    if (next == '.' && !seenDot && i + 1 < source.Length && char.IsDigit(source[i + 1]))
                    {
                        seenDot = true;
                        i++;
                        column++;
                        continue;
                    }

                    break;
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

                while (i < source.Length && IsIdentifierPart(source[i]))
                {
                    if (source[i] == '-' && i + 1 < source.Length && source[i + 1] == '>')
                        break;

                    i++;
                    column++;
                }

                string text = source[start..i];
                HumanReadableTokenKind kind = GetKeywordKind(text);

                tokens.Add(new HumanReadableToken(kind, text, line, startColumn, start, text.Length));
                continue;
            }

            throw WinterForgeSyntaxException.ForToken(
                "Unexpected token.",
                source,
                line,
                column,
                c.ToString(CultureInfo.InvariantCulture));
        }

        tokens.Add(new HumanReadableToken(HumanReadableTokenKind.EndOfFile, string.Empty, line, column, source.Length, 0));
        return tokens;
    }

    public static IReadOnlyList<HumanReadableToken> TokenizeFast(ReadOnlySpan<char> source)
    {
        List<HumanReadableToken> tokens = [];
        int i = 0;
        int line = 1;
        int column = 1;

        while (i < source.Length)
        {
            char c = source[i];

            if (c is '\r' or '\n')
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

            if (c == ' ' || c == '\t')
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

                string text = source[start..i].ToString();
                tokens.Add(new HumanReadableToken(HumanReadableTokenKind.Comment, text, line, startColumn, start, text.Length));
                continue;
            }

            if (c == '#')
            {
                int start = i;
                int startColumn = column;

                i++;
                column++;

                while (i < source.Length && IsIdentifierPart(source[i]))
                {
                    i++;
                    column++;
                }

                string text = source[start..i].ToString();
                tokens.Add(new HumanReadableToken(HumanReadableTokenKind.Directive, text, line, startColumn, start, text.Length));
                continue;
            }

            if (c == '"')
            {
                string sourceString = source.ToString();
                tokens.Add(ReadStringToken(sourceString, ref i, ref line, ref column));
                continue;
            }

            if (TryReadOperatorOrPunctuation(source.ToString(), ref i, ref column, line, out HumanReadableToken opToken))
            {
                tokens.Add(opToken);
                continue;
            }

            if (char.IsDigit(c))
            {
                int start = i;
                int startColumn = column;
                bool seenDot = false;

                i++;
                column++;

                while (i < source.Length)
                {
                    char next = source[i];
                    if (char.IsDigit(next))
                    {
                        i++;
                        column++;
                        continue;
                    }

                    if (next == '.' && !seenDot && i + 1 < source.Length && char.IsDigit(source[i + 1]))
                    {
                        seenDot = true;
                        i++;
                        column++;
                        continue;
                    }

                    break;
                }

                string text = source[start..i].ToString();
                tokens.Add(new HumanReadableToken(HumanReadableTokenKind.Number, text, line, startColumn, start, text.Length));
                continue;
            }

            if (IsIdentifierStart(c))
            {
                int start = i;
                int startColumn = column;

                i++;
                column++;

                while (i < source.Length && IsIdentifierPart(source[i]))
                {
                    if (source[i] == '-' && i + 1 < source.Length && source[i + 1] == '>')
                        break;

                    i++;
                    column++;
                }

                string text = source[start..i].ToString();
                HumanReadableTokenKind kind = GetKeywordKind(text);

                tokens.Add(new HumanReadableToken(kind, text, line, startColumn, start, text.Length));
                continue;
            }

            throw WinterForgeSyntaxException.ForToken(
                "Unexpected token.",
                source.ToString(),
                line,
                column,
                c.ToString(CultureInfo.InvariantCulture));
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
            throw WinterForgeSyntaxException.ForToken(
                "Invalid quote count. Use 1 or 5 quotes.",
                source,
                startLine,
                startColumn,
                source[start..Math.Min(source.Length, start + quoteCount)]);

        bool multiline = quoteCount == 5;
        bool escaped = false;
        int quoteRun = 0;

        while (i < source.Length)
        {
            char c = source[i];

            if (!multiline && c is '\r' or '\n')
                throw WinterForgeSyntaxException.ForToken(
                    "Unterminated single-line string.",
                    source,
                    startLine,
                    startColumn,
                    source[start..Math.Min(source.Length, start + 1)]);

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

        throw WinterForgeSyntaxException.ForToken(
            "Unterminated string.",
            source,
            startLine,
            startColumn,
            source[start..Math.Min(source.Length, start + 1)]);
    }

    private static bool TryReadOperatorOrPunctuation(string source, ref int i, ref int column, int line, out HumanReadableToken token)
    {
        token = default!;

        if (i + 1 < source.Length)
        {
            string two = source.Substring(i, 2);
            if (two is "->" or "=>" or "==" or "!=" or ">=" or "<=" or "&&" or "||" or "**")
            {
                HumanReadableTokenKind kind = two switch
                {
                    "->" => HumanReadableTokenKind.Arrow,
                    "=>" => HumanReadableTokenKind.FatArrow,
                    "==" => HumanReadableTokenKind.DoubleEquals,
                    "!=" => HumanReadableTokenKind.NotEquals,
                    ">=" => HumanReadableTokenKind.GreaterOrEqual,
                    "<=" => HumanReadableTokenKind.LessOrEqual,
                    "&&" => HumanReadableTokenKind.AndAnd,
                    "||" => HumanReadableTokenKind.OrOr,
                    "**" => HumanReadableTokenKind.Caret,
                    _ => HumanReadableTokenKind.Identifier
                };

                token = new HumanReadableToken(kind, two, line, column, i, 2);
                i += 2;
                column += 2;
                return true;
            }
        }

        char one = source[i];
        HumanReadableTokenKind singleKind = one switch
        {
            '+' => HumanReadableTokenKind.Plus,
            '-' => HumanReadableTokenKind.Minus,
            '*' => HumanReadableTokenKind.Star,
            '/' => HumanReadableTokenKind.Slash,
            '%' => HumanReadableTokenKind.Percent,
            '^' => HumanReadableTokenKind.Caret,
            '|' => HumanReadableTokenKind.Pipe,
            '&' => HumanReadableTokenKind.Ampersand,
            '!' => HumanReadableTokenKind.Bang,
            '=' => HumanReadableTokenKind.Equals,
            '>' => HumanReadableTokenKind.GreaterThan,
            '<' => HumanReadableTokenKind.LessThan,
            '.' => HumanReadableTokenKind.Dot,
            ',' => HumanReadableTokenKind.Comma,
            ';' => HumanReadableTokenKind.Semicolon,
            ':' => HumanReadableTokenKind.Colon,
            '(' => HumanReadableTokenKind.OpenParen,
            ')' => HumanReadableTokenKind.CloseParen,
            '{' => HumanReadableTokenKind.OpenBrace,
            '}' => HumanReadableTokenKind.CloseBrace,
            '[' => HumanReadableTokenKind.OpenBracket,
            ']' => HumanReadableTokenKind.CloseBracket,
            _ => HumanReadableTokenKind.Identifier
        };

        if (singleKind == HumanReadableTokenKind.Identifier)
            return false;

        token = new HumanReadableToken(singleKind, one.ToString(), line, column, i, 1);
        i++;
        column++;
        return true;
    }

    private static HumanReadableTokenKind GetKeywordKind(string text)
    {
        return text.ToLowerInvariant() switch
        {
            "return" => HumanReadableTokenKind.KeywordReturn,
            "alias" => HumanReadableTokenKind.KeywordAlias,
            "as" => HumanReadableTokenKind.KeywordAs,
            "inherits" => HumanReadableTokenKind.KeywordInherits,
            "anonymous" => HumanReadableTokenKind.KeywordAnonymous,
            "import" => HumanReadableTokenKind.KeywordImport,
            "if" => HumanReadableTokenKind.KeywordIf,
            "else" => HumanReadableTokenKind.KeywordElse,
            "while" => HumanReadableTokenKind.KeywordWhile,
            "for" => HumanReadableTokenKind.KeywordFor,
            "container" => HumanReadableTokenKind.KeywordContainer,
            "template" => HumanReadableTokenKind.KeywordTemplate,
            "variables" => HumanReadableTokenKind.KeywordVariables,
            "this" => HumanReadableTokenKind.KeywordThis,
            "true" => HumanReadableTokenKind.KeywordTrue,
            "false" => HumanReadableTokenKind.KeywordFalse,
            "null" => HumanReadableTokenKind.KeywordNull,
            "default" => HumanReadableTokenKind.KeywordDefault,
            _ => HumanReadableTokenKind.Identifier
        };
    }

    private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c is '_' or '$';

    private static bool IsIdentifierPart(char c)
        => char.IsLetterOrDigit(c) || c is '_' or '$' or '.';
}
