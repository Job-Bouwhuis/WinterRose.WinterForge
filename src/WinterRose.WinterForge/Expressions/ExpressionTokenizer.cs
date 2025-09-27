using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinterRose.WinterForgeSerializing.Formatting;

namespace WinterRose.WinterForgeSerializing.Expressions;

public enum TokenType
{
    None,
    Number,
    String,
    Identifier,
    Arrow,        // ->
    Operator,     // + - * / etc
    LParen,       // (
    RParen,       // )
    Comma,
}

public class Token(TokenType type, string text)
{
    public TokenType Type => type;
    public string Text { get; set; } = text;
}

public static class ExpressionTokenizer
{
    // Define precedence order (higher number = higher precedence)
    private static readonly Dictionary<string, int> OperatorPrecedence = new()
    {
        {"^", 7},
        {"*", 6}, {"/", 6}, {"%", 6},
        {"+", 5}, {"-", 5},
        {">", 4}, {"<", 4}, {">=", 4}, {"<=", 4},
        {"==", 3}, {"!=", 3},
        {"&&", 2},
        {"||", 1},
        {"!", 8} // unary
    };

    public static List<Token> Tokenize(string input)
    {
        List<Token> tokens = new();
        int i = 0;

        while (i < input.Length)
        {
            char c = input[i];

            // Handle string literals (preserve everything inside quotes)
            if (c == '"')
            {
                int start = i++;
                var sb = new StringBuilder();

                bool escape = false;
                while (i < input.Length)
                {
                    char current = input[i];

                    if (escape)
                    {
                        sb.Append(current switch
                        {
                            'n' => '\n',
                            't' => '\t',
                            'r' => '\r',
                            '\\' => '\\',
                            '"' => '"',
                            '\'' => '\'',
                            _ => current // unknown escapes stay literal
                        });
                        escape = false;
                    }
                    else
                    {
                        if (current == '\\')
                        {
                            escape = true; // next char is escaped
                        }
                        else if (current == '"')
                        {
                            break; // closing quote
                        }
                        else
                        {
                            sb.Append(current);
                        }
                    }

                    i++;
                }

                if (i >= input.Length)
                    throw new Exception("Unterminated string literal");

                i++; // consume closing quote
                tokens.Add(new Token(TokenType.String, sb.ToString()));
                continue;
            }

            if (c == '|' && i + 1 < input.Length)
            {
                int start = i;
                i++; // consume first '|'
                while (i < input.Length && input[i] != '|') i++; // scan type name
                if (i < input.Length && input[i] == '|') i++; // consume closing '|'

                // Now consume the literal value immediately after
                int valueStart = i;
                while (i < input.Length && !char.IsWhiteSpace(input[i]) && "!+-*/%^><=()".IndexOf(input[i]) < 0)
                    i++;

                string fullLiteral = input[start..i];
                tokens.Add(new Token(TokenType.Identifier, fullLiteral));
                continue;
            }

            // Skip whitespace
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            // Arrow (->)
            if (c == '-' && i + 1 < input.Length && input[i + 1] == '>')
            {
                tokens.Add(new Token(TokenType.Arrow, "->"));
                i += 2;
                continue;
            }

            // Multi-char operators
            if (i + 1 < input.Length)
            {
                string twoChar = input.Substring(i, 2);
                if (twoChar is "==" or "!=" or ">=" or "<=" or "&&" or "||")
                {
                    tokens.Add(new Token(TokenType.Operator, twoChar));
                    i += 2;
                    continue;
                }
            }

            // Single-char operators (note: '=' omitted intentionally if you treat '=' as assignment outside expressions)
            if ("+-*/%^><!()".Contains(c))
            {
                if (c == '(')
                    tokens.Add(new Token(TokenType.LParen, "("));
                else if (c == ')')
                    tokens.Add(new Token(TokenType.RParen, ")"));
                else
                    tokens.Add(new Token(TokenType.Operator, c.ToString()));

                i++;
                continue;
            }

            // Comma separator (for method arguments)
            if (c == ',')
            {
                tokens.Add(new Token(TokenType.Comma, ","));
                i++;
                continue;
            }

            // Numbers (including decimals)
            if (char.IsDigit(c) || (c == '.' && i + 1 < input.Length && char.IsDigit(input[i + 1])))
            {
                int start = i;
                bool hasDot = false;

                while (i < input.Length && (char.IsDigit(input[i]) || (!hasDot && input[i] == '.')))
                {
                    if (input[i] == '.')
                        hasDot = true;

                    i++;
                }

                tokens.Add(new Token(TokenType.Number, input[start..i]));
                continue;
            }

            // Identifiers (variables, functions, member chains, etc.)
            // Accepts: Name, Name.Member, Name.Member( ... ), Name->Member, chained combinations
            if (char.IsLetter(c) || c == '_')
            {
                int start = i;

                // Build up the identifier (including -> parts, . member access and method calls)
                string identifier = "";

                while (i < input.Length)
                {
                    int segmentStart = i;

                    // Base identifier segment (letters/digits/underscore)
                    while (i < input.Length && (char.IsLetterOrDigit(input[i]) || input[i] == '_'))
                        i++;

                    identifier += input[segmentStart..i];

                    // Skip whitespace to inspect next char
                    int temp = i;
                    while (temp < input.Length && char.IsWhiteSpace(input[temp])) temp++;

                    // If function call immediately follows, consume the full call (including nested parentheses)
                    if (temp < input.Length && input[temp] == '(')
                    {
                        i = temp;
                        string fullCall = ConsumeFullFunctionCall(input, ref i);
                        identifier += fullCall;
                        // continue the while loop to possibly chain further (e.g. Foo(a)->Bar or Foo(a).Prop)
                        continue;
                    }

                    // If arrow -> continues the chain
                    if (i + 1 < input.Length && input[i] == '-' && input[i + 1] == '>')
                    {
                        identifier += "->";
                        i += 2;
                        continue;
                    }

                    // If dot . continues the member chain (e.g. System.Console.WriteLine)
                    if (i < input.Length && input[i] == '.')
                    {
                        identifier += ".";
                        i++; // consume '.'
                        continue;
                    }

                    // nothing more to chain
                    break;
                }

                tokens.Add(new Token(TokenType.Identifier, identifier));
                continue;
            }

            // if nothing matched, throw
            throw new Exception($"Unexpected character '{c}' at position {i}");
        }

        // NOTE: original code returned postfix tokens — that's fine for expression evaluation.
        // We'll simply return the token list converted to postfix here to preserve existing behavior (operators handled).
        return ConvertToPostfix(tokens);
    }

    private static string ConsumeFullFunctionCall(string input, ref int i)
    {
        int start = i;
        int depth = 0;
        bool insideQuotes = false;

        while (i < input.Length)
        {
            char current = input[i];

            if (current == '"')
            {
                bool escaped = i > 0 && input[i - 1] == '\\';
                if (!escaped)
                    insideQuotes = !insideQuotes;
            }

            if (!insideQuotes)
            {
                if (current == '(')
                {
                    depth++;
                }
                else if (current == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        i++; // include closing ')'
                        break;
                    }
                }
            }

            i++;
        }

        return input[start..i];
    }

    private static bool IsOperator(Token token) => token.Type == TokenType.Operator;

    private static int GetPrecedence(string op) => OperatorPrecedence.TryGetValue(op, out int prec) ? prec : 0;

    private static bool IsRightAssociative(string op) => op == "^" || op == "!";

    private static List<Token> ConvertToPostfix(List<Token> tokens)
    {
        var output = new List<Token>();
        var operators = new Stack<Token>();

        for (int i = 0; i < tokens.Count; i++)
        {
            Token token = tokens[i];

            switch (token.Type)
            {
                case TokenType.Number:
                case TokenType.Identifier:
                case TokenType.String:
                    output.Add(token);
                    break;

                case TokenType.Operator:
                    {
                        while (operators.Count > 0 && IsOperator(operators.Peek()))
                        {
                            var topOp = operators.Peek();
                            int topPrec = GetPrecedence(topOp.Text);
                            int currentPrec = GetPrecedence(token.Text);

                            if ((IsRightAssociative(token.Text) && currentPrec < topPrec) ||
                                (!IsRightAssociative(token.Text) && currentPrec <= topPrec))
                            {
                                output.Add(operators.Pop());
                            }
                            else
                            {
                                break;
                            }
                        }

                        operators.Push(token);
                        break;
                    }

                case TokenType.LParen:
                    operators.Push(token);
                    break;

                case TokenType.RParen:
                    while (operators.Count > 0 && operators.Peek().Type != TokenType.LParen)
                    {
                        output.Add(operators.Pop());
                    }
                    if (operators.Count == 0)
                        throw new Exception("Mismatched parentheses");
                    operators.Pop(); // Remove '('
                    break;

                case TokenType.Arrow:
                    // treat arrow as higher-precedence op, but many consumers expect arrow chains to be identifiers
                    operators.Push(token);
                    break;

                default:
                    throw new Exception($"Unexpected token {token.Type} in expression");
            }
        }

        while (operators.Count > 0)
        {
            var top = operators.Pop();
            if (top.Type == TokenType.LParen || top.Type == TokenType.RParen)
                throw new Exception("Mismatched parentheses");
            output.Add(top);
        }

        return output;
    }
}

/// <summary>
/// Small helpers that use the tokenizer to detect whether a string contains a "true expression"
/// (i.e. arithmetic/boolean/comparison operators) outside of quotes.
/// </summary>
public static class ExpressionUtils
{
    // operators that indicate an expression (single '=' is typically assignment and ignored)
    private static readonly HashSet<string> ExpressionOperators = new()
    {
        "+","-","*","/","%","^",
        ">", "<", ">=", "<=", "==", "!=",
        "&&", "||", "!"
    };

    /// <summary>
    /// Returns true if the input contains expression operators (math/boolean/comparison) outside quotes.
    /// Member access chains and single function calls (no operators) will return false.
    /// </summary>
    public static bool ContainsExpressionOutsideQuotes(string input)
    {
        if (string.IsNullOrEmpty(input))
            return false;

        List<Token> tokens;
        try
        {
            // Tokenize returns postfix converted tokens in this implementation,
            // but operator tokens will still be present.
            tokens = ExpressionTokenizer.Tokenize(input);
        }
        catch
        {
            // If tokenization fails, conservatively fall back to a safer scan:
            // check for operator chars outside quotes (but ignore '.' and '->').
            bool insideQuotes = false;
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (c == '"')
                {
                    bool escaped = i > 0 && input[i - 1] == '\\';
                    if (!escaped) insideQuotes = !insideQuotes;
                    continue;
                }
                if (insideQuotes) continue;
                // treat only real expression operator characters:
                if ("+-*/%><!&|^".Contains(c)) return true;
            }
            return false;
        }

        // If any operator token is present that is in ExpressionOperators -> it's an expression.
        foreach (var t in tokens)
        {
            if (t.Type == TokenType.Operator && ExpressionOperators.Contains(t.Text))
                return true;
        }

        // No expression-like operators => not an expression (member chains, single identifiers or function calls).
        return false;
    }
}
