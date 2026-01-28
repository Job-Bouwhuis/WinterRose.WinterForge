namespace WinterRose.WinterForgeSerializing.Factory;

public sealed class MapNode : Node
{
    public Type KeyType { get; }
    public Type ValueType { get; }
    public List<MapEntryNode> Entries { get; } = new();

    public MapNode(int id, Type keyType, Type valueType)
        : base(id)
    {
        KeyType = keyType;
        ValueType = valueType;
    }
}