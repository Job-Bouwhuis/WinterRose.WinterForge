using System.Diagnostics.CodeAnalysis;
using WinterRose.WinterForgeSerializing.Workers;

namespace WinterRose.WinterForgeSerializing.Containers;

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

    public TemplateGroup DeepCopy(Scope newParent)
    {
        var copy = new TemplateGroup(Name);
        copy.Templates.AddRange(Templates.Select(t => (Template)t.DeepCopy(newParent)));
        return copy;
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
                Type p1 = parameters[i].Type;
                Type p2 = tp.Parameters[i].Type;

                if (p1.Equals(p2) || p1.IsAssignableTo(p2))
                    return false;
            }
        }
        return true;
    }

    public bool TryCall(out object? returnValue, List<object> args, WinterForgeVM executor, bool useImplicitEmptyCall = false)
    {
        var resolved = DynamicObjectCreator.ResolveArgumentTypes(args.ToList());

        foreach (var template in Templates)
        {
            if (template.Parameters.Count != resolved.Count)
                continue;

            var targetTypes = template.Parameters.Select(p => p.Type).ToArray();

            if (!DynamicObjectCreator.TryConvertArguments(resolved, targetTypes, out object[] convertedArgs))
                continue;

            using (executor.PushScope(template))
                returnValue = template.Call(convertedArgs, executor);

            return true;
        }

        returnValue = null;
        return useImplicitEmptyCall;
    }

    internal void Clear()
    {
        Templates.Clear();
    }
}


