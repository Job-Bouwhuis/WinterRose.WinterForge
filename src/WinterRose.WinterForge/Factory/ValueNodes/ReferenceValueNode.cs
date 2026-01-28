namespace WinterRose.WinterForgeSerializing.Factory;

public sealed class ReferenceValueNode : ValueNode
{
    public Handle Target { get; }

    public ReferenceValueNode(Handle target)
    {
        Target = target;
    }
}