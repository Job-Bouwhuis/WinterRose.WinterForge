using System;
using System.Text;

namespace WinterRose.WinterForgeSerializing.Formatting
{
    /// <summary>
    /// Tracks the current parsing location for Rust-style error reporting.
    /// </summary>
    internal class ParserErrorContext
    {
        private int currentLineNumber = 1;
        private int currentColumnNumber = 1;
        private string? currentLineContent = null;

        public int LineNumber => currentLineNumber;
        public int ColumnNumber => currentColumnNumber;
        public string? LineContent => currentLineContent;

        /// <summary>
        /// Updates the line number and resets column to 1.
        /// </summary>
        public void AdvanceLine(string lineContent)
        {
            currentLineNumber++;
            currentColumnNumber = 1;
            currentLineContent = lineContent;
        }

        /// <summary>
        /// Updates the column position within the current line.
        /// </summary>
        public void SetColumnInLine(string line, int charIndex)
        {
            currentLineContent = line;
            currentColumnNumber = Math.Min(charIndex + 1, line.Length + 1);
        }

        /// <summary>
        /// Generates a Rust-style error message with context visualization.
        /// </summary>
        public string FormatError(string message, string? contextLine = null)
        {
            contextLine ??= currentLineContent;

            var sb = new StringBuilder();
            sb.AppendLine($"error: {message}");
            sb.AppendLine($"  --> line {currentLineNumber}:{currentColumnNumber}");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(contextLine))
            {
                // Show line number and content
                sb.AppendLine($" {currentLineNumber:D4} | {contextLine}");

                // Show pointer to the error location
                int pointerPos = currentColumnNumber - 1;
                if (pointerPos >= 0 && pointerPos <= contextLine.Length)
                {
                    sb.Append("      | ");
                    for (int i = 0; i < pointerPos; i++)
                    {
                        sb.Append(contextLine[i] == '\t' ? '\t' : ' ');
                    }
                    sb.AppendLine("^");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Generates a Rust-style error with multiple context lines (before and after).
        /// </summary>
        public string FormatErrorWithContext(string message, string? contextLine = null, int contextLinesBefore = 1, int contextLinesAfter = 1)
        {
            contextLine ??= currentLineContent;

            var sb = new StringBuilder();
            sb.AppendLine($"error: {message}");
            sb.AppendLine($"  --> line {currentLineNumber}:{currentColumnNumber}");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(contextLine))
            {
                // Show the actual error line
                sb.AppendLine($" {currentLineNumber:D4} | {contextLine}");

                // Show pointer to the error location
                int pointerPos = currentColumnNumber - 1;
                if (pointerPos >= 0 && pointerPos <= contextLine.Length)
                {
                    sb.Append("      | ");
                    for (int i = 0; i < pointerPos; i++)
                    {
                        sb.Append(contextLine[i] == '\t' ? '\t' : ' ');
                    }
                    sb.AppendLine("^");
                }
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Enhanced exception with Rust-style error formatting.
    /// </summary>
    public class WinterForgeParseException : Exception
    {
        public int LineNumber { get; }
        public int ColumnNumber { get; }
        public string? ContextLine { get; }

        public WinterForgeParseException(string message) : base(message)
        {
            LineNumber = -1;
            ColumnNumber = -1;
            ContextLine = null;
        }

        public WinterForgeParseException(string message, int lineNumber, int columnNumber, string? contextLine = null)
            : base(FormatErrorMessage(message, lineNumber, columnNumber, contextLine))
        {
            LineNumber = lineNumber;
            ColumnNumber = columnNumber;
            ContextLine = contextLine;
        }

        private static string FormatErrorMessage(string message, int lineNumber, int columnNumber, string? contextLine)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"error: {message}");
            sb.AppendLine($"  --> line {lineNumber}:{columnNumber}");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(contextLine))
            {
                sb.AppendLine($" {lineNumber:D4} | {contextLine}");

                int pointerPos = columnNumber - 1;
                if (pointerPos >= 0 && pointerPos <= contextLine.Length)
                {
                    sb.Append("      | ");
                    for (int i = 0; i < pointerPos; i++)
                    {
                        sb.Append(contextLine[i] == '\t' ? '\t' : ' ');
                    }
                    sb.Append("^");

                    // Extend pointer to show length of problematic token if possible
                    int remainingLen = contextLine.Length - pointerPos;
                    if (remainingLen > 1)
                    {
                        int tokenLen = 0;
                        for (int i = pointerPos; i < contextLine.Length && (char.IsLetterOrDigit(contextLine[i]) || contextLine[i] == '_'); i++)
                            tokenLen++;

                        if (tokenLen > 1)
                        {
                            for (int i = 1; i < tokenLen; i++)
                                sb.Append('-');
                        }
                    }
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }
    }
}
