namespace WinterRose.WinterForgeSerializing.Factory;

public abstract class Handle
{
    internal int Id { get; }
    internal Node Node { get; }

    protected Handle(int id, Node node)
    {
        Id = id;
        Node = node;
    }
}