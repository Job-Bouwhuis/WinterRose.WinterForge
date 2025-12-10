namespace WinterRose.WinterForgeSerializing.Containers;

public abstract class Scope
{
    public Scope? Parent { get; set; }

    public string Name { get; set; }
    public Dictionary<string, Variable> Variables { get; set; } = [];
    public Dictionary<string, TemplateGroup> Templates { get; set; } = [];
    public Dictionary<string, Container> Containers { get; set; } = [];

    public void DefineTemplate(Template template)
    {
        template.Parent = this;

        if (Templates.TryGetValue(template.Name, out var group))
        {
            group.DefineTemplate(template);
            return;
        }

        TemplateGroup newGroup = new TemplateGroup(template.Name);
        Templates[template.Name] = newGroup;
        newGroup.DefineTemplate(template);
    }

    public void DefineVariable(Variable var) => Variables[var.Name] = var;

    public object? GetIdentifier(string name)
    {
        if (Variables.TryGetValue(name, out var var))
            return var;

        if(Templates.TryGetValue(name, out var group))
            return group;

        if(Parent is not null)
            return Parent.GetIdentifier(name);
        return null;
    }

    public virtual Scope DeepCopy(Scope newParent)
    {
        var copy = (Scope)MemberwiseClone();
        copy.Parent = newParent;
        copy.Variables = Variables.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.DeepCopy());
        copy.Templates = Templates.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.DeepCopy(copy));
        return copy;
    }
}


