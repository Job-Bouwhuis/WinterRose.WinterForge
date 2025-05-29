using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinterRose.WinterForgeSerializing.Formatting
{
    /// <summary>
    /// Indents the human readable format created by <see cref="WinterForge"/>
    /// </summary>
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
                bool alreadyTrimmed = false;
                if(line.Trim() == "}")
                {
                    UpdateIndentation(line);
                    alreadyTrimmed = true;
                }

                string indentedLine = GetIndentedLine(line);
                if(!string.IsNullOrWhiteSpace(indentedLine))
                    _outputStreamWriter.WriteLine(indentedLine);

                if(!alreadyTrimmed)
                    UpdateIndentation(line);
            }

            _outputStreamWriter.Flush();
        }

        private string GetIndentedLine(string line) => new string(INDENT_CHAR, _currentIndentLevel) + line;

        private void UpdateIndentation(string line)
        {
            if (line.Contains('{') || line.Contains('['))
                _currentIndentLevel++;

            if (line.Contains('}') || line.Contains(']'))
                _currentIndentLevel = Math.Max(0, _currentIndentLevel - 1);
        }
    }
}
