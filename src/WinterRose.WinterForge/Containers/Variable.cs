using WinterRose.WinterForgeSerializing.Instructions;

namespace WinterRose.WinterForgeSerializing.Containers;

public class Variable
{
    public Variable(string name)
    {
        Name = name;
    }
    public Variable() { }

    public string Name { get; set; }
    public object Value { get; set; }
    internal List<Instruction>? DefaultValueInstructions { get; set; }
    public object? defaultValue { get; set; }
    public bool DefaultValueAsExpression { get; set; }

    public Variable DeepCopy()
    {
        return new Variable
        {
            Name = Name,
            Value = Value is ICloneable cloneable ? cloneable.Clone() : Value, // shallow if not cloneable
            DefaultValueInstructions = DefaultValueInstructions?.Select(i => i.Clone()).ToList(), // assuming Instruction has Clone()
            defaultValue = defaultValue is ICloneable c ? c.Clone() : defaultValue,
            DefaultValueAsExpression = DefaultValueAsExpression
        };
    }
}


