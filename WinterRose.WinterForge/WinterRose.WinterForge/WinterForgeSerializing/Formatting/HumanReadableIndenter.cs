using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinterRose.WinterForgeSerializing.Formatting
{
    public class HumanReadableIndenter
    {
        private StreamReader _inputStreamReader;
        private StreamWriter _outputStreamWriter;
        private int _currentIndentLevel = 0;
        private const char INDENT_CHAR = '\t';

        public void Process(Stream inputStream, Stream outputStream)
        {
            _inputStreamReader = new StreamReader(inputStream);
            _outputStreamWriter = new StreamWriter(outputStream, Encoding.UTF8);

            string? line;
            while ((line = _inputStreamReader.ReadLine()) != null)
            {
                // Apply the current indentation level to the line
                string indentedLine = GetIndentedLine(line);
                if(!string.IsNullOrWhiteSpace(indentedLine))
                    _outputStreamWriter.WriteLine(indentedLine);

                // Update the indentation level based on braces and brackets
                UpdateIndentation(line);
            }

            // Ensure all output is written to the stream
            _outputStreamWriter.Flush();
        }

        private string GetIndentedLine(string line)
        {
            // Add the correct indentation before the line
            return new string(INDENT_CHAR, _currentIndentLevel) + line;
        }

        private void UpdateIndentation(string line)
        {
            // Increase indent when encountering '{' or '['
            if (line.Contains('{') || line.Contains('['))
                _currentIndentLevel++;

            // Decrease indent when encountering '}' or ']'
            if (line.Contains('}') || line.Contains(']'))
                _currentIndentLevel = Math.Max(0, _currentIndentLevel - 1);
        }
    }
}
