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
    public class WinterForgeFormatException(string syntaxPart, string reason = "")
        : Exception($"Syntax '{syntaxPart}' is not correct: " + reason);
}
