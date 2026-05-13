using System.Globalization;
using System.Text;

namespace WinterRose.WinterForgeSerializing.Formatting;

public sealed class WinterForgeSyntaxException : WinterForgeFormatException
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

    private WinterForgeSyntaxException(
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

    internal static WinterForgeSyntaxException ForToken(string reason, string source, int line, int column, string token, string? suggestion = null)
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

        return new WinterForgeSyntaxException(
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
