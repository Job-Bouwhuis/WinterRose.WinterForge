namespace WinterRose.WinterForgeSerializing.Factory;

public sealed class ObjectHandle : Handle
{
    public Type ObjectType { get; }

    internal ObjectHandle(int id, ObjectNode node, Type type)
        : base(id, node)
    {
        ObjectType = type;
    }

    public void DefineMember(string name, ValueNode value)
    {
        ((ObjectNode)Node).Members.Add(new MemberNode(name, value));
    }
    public void DefineMember(string name, object value)
    {
        ((ObjectNode)Node).Members.Add(new MemberNode(name, WinterForgeFactory.ValueFrom(value)));
    }
}