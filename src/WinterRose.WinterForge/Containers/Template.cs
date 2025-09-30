using System.Linq;
using WinterRose.WinterForgeSerializing.Instructions;
using WinterRose.WinterForgeSerializing.Workers;

namespace WinterRose.WinterForgeSerializing.Containers;

public class Template : Scope
{
    public List<Instruction> Instructions { get; set; } = [];
    public List<TemplateParmeter> Parameters { get; set; } = [];

    public Template(string name, params List<TemplateParmeter> parameters)
    {
        Name = name;
        Parameters = parameters;
    }

    public override string ToString() => $"#template {Name} " + string.Join(", ", Parameters.Select(p => {
        return $"{p.Type.FullName} {p.Name}";
    }));

    public override Scope DeepCopy(Scope newParent)
    {
        var copy = (Template)base.DeepCopy(newParent);
        copy.Name = Name;
        copy.Parameters = Parameters.Select(p => p.DeepCopy()).ToList();
        return copy;
    }

    internal object? Call(object[] args, WinterForgeVM executor)
    {
        if (args.Length != Parameters.Count)
            throw new WinterForgeExecutionException($"Parameter count mismatch. Expected " +
                $"[{string.Join(", ", Parameters.Select(p => p.Type.FullName))}]" +
                $" but got [{string.Join(", ", args.Select(a => a.GetType().FullName))}]");

        for (int i = 0; i < Parameters.Count; i++)
        {
            string name = Parameters[i].Name;
            Variable v = new(name);
            v.Value = args[i];
            DefineVariable(v);
        }

        return executor.Execute(Instructions, false);
    }
}


