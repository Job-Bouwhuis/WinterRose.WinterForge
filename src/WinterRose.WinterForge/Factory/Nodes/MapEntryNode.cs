namespace WinterRose.WinterForgeSerializing.Factory;

public sealed class MapEntryNode
{
    public ValueNode Key { get; }
    public ValueNode Value { get; }

    public MapEntryNode(ValueNode key, ValueNode value)
    {
        Key = key;
        Value = value;
    }
}