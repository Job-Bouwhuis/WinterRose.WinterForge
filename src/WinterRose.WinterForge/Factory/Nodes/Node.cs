namespace WinterRose.WinterForgeSerializing.Factory;

public abstract class Node
{
    public int Id { get; }

    protected Node(int id)
    {
        Id = id;
    }
}