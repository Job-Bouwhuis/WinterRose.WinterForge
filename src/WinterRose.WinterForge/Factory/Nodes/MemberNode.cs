namespace WinterRose.WinterForgeSerializing.Factory;

public sealed class MemberNode
{
    public string Name { get; }
    public ValueNode Value { get; }

    public MemberNode(string name, ValueNode value)
    {
        Name = name;
        Value = value;
    }
}