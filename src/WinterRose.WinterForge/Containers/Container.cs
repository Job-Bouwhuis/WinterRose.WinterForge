using System.Collections.Generic;
using System.Runtime.CompilerServices;
using WinterRose.WinterForgeSerializing.Workers;

namespace WinterRose.WinterForgeSerializing.Containers;

public class Container : Scope
{
    public TemplateGroup Constructors { get; } = new("ctor");

    public bool CreateInstance(List<object> consturctorArgs, WinterForgeVM VM)
    {
        isInstance = true;
        return Constructors.TryCall(out _, consturctorArgs, VM, true);
    }

    public bool isInstance { get; private set; } = false;

    public Container(string name) => Name = name;

    public override Scope DeepCopy(Scope newParent)
    {
        var copy = (Container)base.DeepCopy(newParent);
        copy.Constructors.Templates = Constructors.Templates.Select(t => t.DeepCopy(copy)).Cast<Template>().ToList();
        return copy;
    }
}