namespace WinterRose.WinterForgeSerializing.Factory;

public sealed class InlineObjectValueNode : ValueNode
{
    public ObjectNode Object { get; }

    public InlineObjectValueNode(ObjectNode obj)
    {
        Object = obj;
    }
}