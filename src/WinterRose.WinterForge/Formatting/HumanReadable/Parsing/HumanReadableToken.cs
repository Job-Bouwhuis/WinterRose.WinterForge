using System.Diagnostics;

namespace WinterRose.WinterForgeSerializing.Formatting.HumanReadable.Parsing;

[DebuggerDisplay("{Kind} {Text} ({Line},{Column})")]
internal sealed record HumanReadableToken(
    HumanReadableTokenKind Kind,
    string Text,
    int Line,
    int Column,
    int Start,
    int Length);
