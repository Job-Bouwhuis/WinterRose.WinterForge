using System.Text;
using WinterRose.WinterForgeSerializing.Formatting.HumanReadable.Parsing;

namespace WinterRose.WinterForgeSerializing.Formatting;

internal static class HumanReadableAstSourceEmitter
{
    public static string Emit(HumanReadableProgramNode program)
    {
        if (program.Statements.Count == 0)
            return string.Empty;

        StringBuilder sb = new();
        for (int i = 0; i < program.Statements.Count; i++)
        {
            if (i > 0)
                sb.AppendLine();

            sb.Append(program.Statements[i].Text.Trim());
        }

        return sb.ToString();
    }
}
