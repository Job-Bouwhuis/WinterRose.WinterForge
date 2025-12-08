using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinterRose.WinterForgeSerializing
{
    /// <summary>
    /// Thrown when the format trying to parse or deserialize was not correct
    /// </summary>
    public class WinterForgeFormatException : Exception
    {
        public WinterForgeFormatException(string syntaxPart, string reason = "") 
            : base($"Syntax '{syntaxPart}' is not correct" + (string.IsNullOrEmpty(reason) ? (": " + reason) : ""))
        {
        }

        public WinterForgeFormatException(string reason)
            : base($"Format is not correct: {reason}")
        {

        }
    }
}
