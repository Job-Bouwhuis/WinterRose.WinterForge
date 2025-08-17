using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinterRose.WinterForgeSerializing;

namespace WinterRose.WinterForgeSerializing.Formatting
{
    /// <summary>
    /// What format should <see cref="WinterForge"/> serialize to
    /// </summary>
    public enum TargetFormat
    {
        /// <summary>
        /// Plain text format, easer to read than <see cref="Optimized"/> but minimal formatting.
        /// </summary>
        HumanReadable,

        /// <summary>
        /// Text format with indentation for better readability.
        /// </summary>
        FormattedHumanReadable,

        /// <summary>
        /// Textual opcode format. think IL for .NET, but for WinterForge.
        /// </summary>
        IntermediateRepresentation,

        /// <summary>
        /// Bytecode format optimized for speed.
        /// </summary>
        Optimized
    }

}
