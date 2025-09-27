using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace WinterRose.WinterForgeSerializing.Containers;

public class Container : Scope
{
    public TemplateGroup Constructors { get; } = new("ctor");

    public Container(string name) => Name = name;

    public override Scope DeepCopy(Scope newParent)
    {
        var copy = (Container)base.DeepCopy(newParent);
        copy.Constructors.Templates = Constructors.Templates.Select(t => t.DeepCopy(copy)).Cast<Template>().ToList();
        return copy;
    }
}