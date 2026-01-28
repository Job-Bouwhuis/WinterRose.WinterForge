namespace WinterRose.WinterForgeSerializing.Factory;

public sealed class PrimitiveValueNode : ValueNode
{
    public object? Value { get; }

    public PrimitiveValueNode(object? value)
    {
        Value = value;
    }
}