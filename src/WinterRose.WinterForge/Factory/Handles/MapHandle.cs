namespace WinterRose.WinterForgeSerializing.Factory;

public sealed class MapHandle : Handle
{
    public Type KeyType { get; }
    public Type ValueType { get; }

    internal MapHandle(int id, MapNode node, Type keyType, Type valueType)
        : base(id, node)
    {
        KeyType = keyType;
        ValueType = valueType;
    }

    public void Add(ValueNode key, ValueNode value)
    {
        ((MapNode)Node).Entries.Add(new MapEntryNode(key, value));
    }
}