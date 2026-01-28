namespace WinterRose.WinterForgeSerializing.Factory;

public sealed class ObjectNode : Node
{
    public Type ObjectType { get; }
    public List<MemberNode> Members { get; } = new();

    public ObjectNode(int id, Type type)
        : base(id)
    {
        ObjectType = type;
    }
}