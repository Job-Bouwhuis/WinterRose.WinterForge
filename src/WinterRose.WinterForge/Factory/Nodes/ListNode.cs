namespace WinterRose.WinterForgeSerializing.Factory;

public sealed class ListNode : Node
{
    public Type ElementType { get; }
    public List<ValueNode> Elements { get; } = new();

    public ListNode(int id, Type elementType)
        : base(id)
    {
        ElementType = elementType;
    }
}