using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinterRose.WinterForgeSerializing.Expressions;

public enum TokenType
{
    Number,
    String,
    Identifier,
    Arrow,        // ->
    Operator,     // + - * /
    LParen,       // (
    RParen,       // )
    Comma,
}

public record Token(TokenType Type, string Text);

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
        {"!", 8} // Unary operators highest? Treat carefully
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
                while (i < input.Length && input[i] != '"')
                    i++;

                if (i >= input.Length)
                    throw new Exception("Unterminated string literal");

                i++; // Consume the closing quote
                tokens.Add(new Token(TokenType.String, input[start..i]));
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

            // Single-char operators
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

            // Identifiers (variables, functions, etc.)
            if (char.IsLetter(c) || c == '_')
            {
                int start = i;

                // Build up the identifier (including -> parts and method calls)
                string identifier = "";

                while (i < input.Length)
                {
                    int segmentStart = i;

                    // Base identifier
                    while (i < input.Length && (char.IsLetterOrDigit(input[i]) || input[i] == '_'))
                        i++;

                    identifier += input[segmentStart..i];

                    // Check for function call after identifier
                    int temp = i;
                    while (temp < input.Length && char.IsWhiteSpace(input[temp])) temp++;

                    if (temp < input.Length && input[temp] == '(')
                    {
                        i = temp;
                        string fullCall = ConsumeFullFunctionCall(input, ref i);
                        identifier += fullCall;
                    }

                    // Check for -> to continue the chain
                    if (i + 1 < input.Length && input[i] == '-' && input[i + 1] == '>')
                    {
                        identifier += "->";
                        i += 2;
                    }
                    else
                    {
                        break;
                    }
                }

                tokens.Add(new Token(TokenType.Identifier, identifier));
                continue;
            }


            throw new Exception($"Unexpected character '{c}' at position {i}");
        }

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

    private static List<Token> CombineChainedIdentifiers(List<Token> tokens)
    {
        var combined = new List<Token>();

        for (int i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Type == TokenType.Identifier)
            {
                // Start building a chain
                var sb = new StringBuilder(tokens[i].Text);

                int j = i + 1;
                while (j + 1 < tokens.Count && tokens[j].Type == TokenType.Arrow && tokens[j + 1].Type == TokenType.Identifier)
                {
                    sb.Append("->");
                    sb.Append(tokens[j + 1].Text);
                    j += 2; // Skip the arrow and next identifier
                }

                combined.Add(new Token(TokenType.Identifier, sb.ToString()));
                i = j - 1; // Move i forward to the last processed token in the chain
            }
            else
            {
                combined.Add(tokens[i]);
            }
        }

        return combined;
    }


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
                        // Handle unary operators (like !) here if needed
                        // (optional: check if previous token is operator or left paren or start of expression)

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
                    // Treat as operator with very high precedence, or handle separately during parsing.
                    // For now just push it, you may want special handling.
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

