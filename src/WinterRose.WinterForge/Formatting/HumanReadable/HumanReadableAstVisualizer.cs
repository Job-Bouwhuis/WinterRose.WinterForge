using System.Text;
using WinterRose.WinterForgeSerializing.Expressions;
using WinterRose.WinterForgeSerializing.Formatting.HumanReadable.Parsing;

namespace WinterRose.WinterForgeSerializing.Formatting;

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
