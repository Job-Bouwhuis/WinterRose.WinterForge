using System.Collections.Generic;
using WinterRose.WinterForgeSerializing;

namespace WinterForgeTests;

public class ComplexParserTestClass
{
    public string name { get; set; } = string.Empty;
    public List<int> numbers { get; set; } = [];
    public List<string> tags { get; set; } = [];
    public Dictionary<string, int> scores { get; set; } = [];
    public TargetFormat format { get; set; }
}
