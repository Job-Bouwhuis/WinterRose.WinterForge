namespace WinterRose.WinterForgeSerializing.Factory;

public sealed class ListHandle : Handle
{
    public Type ElementType { get; }

    internal ListHandle(int id, ListNode node, Type elementType)
        : base(id, node)
    {
        ElementType = elementType;
    }

    public void Add(ValueNode value)
    {
        ((ListNode)Node).Elements.Add(value);
    }
}