using System.Diagnostics.CodeAnalysis;
using WinterRose.WinterForgeSerializing.Instructions;

namespace WinterRose.WinterForgeSerializing.Containers;

public class Container
{
    public Dictionary<string, object> Variables { get; set; } = [];
    public TemplateGroup Constructors { get; } = new("ctor");
    public Dictionary<string, TemplateGroup> Templates { get; set; } = [];

    public string Name { get; set; }

    public Container(string name)
    {
        Name = name;
    }

    public void DefineTemplate(Template template)
    {
        if (Templates.TryGetValue(template.Name, out var group))
        {
            group.DefineTemplate(template);
            return;
        }

        TemplateGroup newGroup = new TemplateGroup(template.Name);
        Templates[template.Name] = newGroup;
        newGroup.DefineTemplate(template);
    }
}

public class TemplateGroup(string name)
{
    public string Name => name;

    public List<Template> Templates { get; set; } = [];

    public void DefineTemplate(Template t)
    {
        if (Templates.Contains(t))
            throw new InvalidOperationException("WF-C Template already exists within the template group");

        if (!ValidateTemplate(t, out string? error))
            throw new InvalidOperationException(error);

        Templates.Add(t);
    }

    private bool ValidateTemplate(Template t, [NotNullWhen(false)] out string? error)
    {
        error = string.Empty;
        if (!ContainsTemplateSignature(t))
        {
            error = "WF-C Ambiguous Template Definition for: " + t.ToString();
            return false;
        }
        return true;
    }

    private bool ContainsTemplateSignature(Template t)
    {
        var parameters = t.Parameters;
        foreach (var tp in Templates)
        {
            if (tp.Parameters.Count != parameters.Count)
                continue;

            for (int i = 0; i < parameters.Count; i++)
            {
                Type p1 = parameters[i].type;
                Type p2 = tp.Parameters[i].type;

                if (p1.Equals(p2) || p1.IsAssignableTo(p2))
                    return false;
            }
        }
        return true;
    }
}

public class Template
{
    public List<Instruction> Instructions { get; set; } = [];
    public List<ParamTuple> Parameters { get; set; } = [];

    public string Name { get; set; }

    public Template(string name, params List<ParamTuple> parameters)
    {
        Name = name;
        Parameters = parameters;
    }

    public override string ToString() => $"#template {Name} " + string.Join(", ", Parameters.Select(p => {
        return $"{p.type.FullName} {p.name}";
    }));
}
