namespace WinterRose.WinterForgeSerializing.Containers;

public struct TemplateParmeter
{
    public Type Type { get; set; }
    public string Name { get; set; }

    public TemplateParmeter(Type type, string name) => (Type, Name) = (type, name);

    public TemplateParmeter DeepCopy() => new TemplateParmeter(Type, Name);
}


