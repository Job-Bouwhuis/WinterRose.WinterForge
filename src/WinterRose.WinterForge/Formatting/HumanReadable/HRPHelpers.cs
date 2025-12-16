using System.Globalization;
using System.Text;
using WinterRose.WinterForgeSerializing.Expressions;

namespace WinterRose.WinterForgeSerializing.Formatting;

internal static class HRPHelpers
{

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
        if (string.IsNullOrWhiteSpace(parts[1]) && !string.IsNullOrWhiteSpace(parts[2]) && parts[2] is not ";" and { Length: > 1 })
        {
            (parts[1], parts[2]) = (parts[2], parts[1]);
        }
        if (parts[2].Length > 1 && !parts[2].EndsWith(';'))
            parts[2] += ';';
        if (parts[2] is ";")
            parts[2] = "";

        return parts;
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

    public static List<string> SplitPreserveQuotes(string input, char separator)
    {
        List<string> parts = new List<string>();
        StringBuilder current = new StringBuilder();

        bool inQuotes = false;
        bool escape = false;

        foreach (char c in input)
        {
            if (escape)
            {
                current.Append(c);
                escape = false;
            }
            else if (c == '\\')
            {
                escape = true;
            }
            else if (c == '"')
            {
                inQuotes = !inQuotes;
                current.Append(c);
            }
            else if (c == separator && !inQuotes)
            {
                parts.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        // add the last part
        if (current.Length > 0)
            parts.Add(current.ToString());

        return parts;
    }

    public static List<string> SplitPreserveQuotesAndParentheses(string input, char separator)
    {
        List<string> parts = new List<string>();
        StringBuilder current = new StringBuilder();

        bool inQuotes = false;
        bool escape = false;
        int parenDepth = 0;

        foreach (char c in input)
        {
            if (escape)
            {
                current.Append(c);
                escape = false;
            }
            else if (c == '\\')
            {
                escape = true;
            }
            else if (c == '"')
            {
                inQuotes = !inQuotes;
                current.Append(c);
            }
            else if (c == '(' && !inQuotes)
            {
                parenDepth++;
                current.Append(c);
            }
            else if (c == ')' && !inQuotes)
            {
                parenDepth--;
                current.Append(c);
            }
            else if (c == separator && !inQuotes && parenDepth == 0)
            {
                // Split only if we're not in quotes and not inside parentheses
                parts.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        // add the last part
        if (current.Length > 0)
            parts.Add(current.ToString().Trim());

        return parts;
    }
    public static bool HasValidGenericFollowedByBracket(ReadOnlySpan<char> input)
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

    public static int ContainsSequenceOutsideBraces(StringBuilder sb, string sequence)
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
    
    public static int ContainsSequenceOutsideQuotes(string text, string sequence)
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
    public static bool IsMethodCall(string line)
    {
        return HRPHelpers.ContainsSequenceOutsideQuotes(line, "(") != -1 && line.EndsWith(')');
    }
}