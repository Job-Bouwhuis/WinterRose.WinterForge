using System.Text;
using WinterRose.WinterForgeSerializing.Compiling;
using WinterRose.WinterForgeSerializing.Containers;
using WinterRose.WinterForgeSerializing.Workers;

namespace WinterRose.WinterForgeSerializing.Attributes;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class SkipWhenAttribute : Attribute
{
    private const string TEMPLATE_START = "#template SkipIf object actual";
    private const string TEMPLATE_END = "return SkipIf;";
    private readonly object value;
    private readonly WinterForgeVM? vm;

    public SkipWhenAttribute(object value)
    {
        if (value is string s && s.StartsWith(TEMPLATE_START))
        {
            if(!s.EndsWith(TEMPLATE_END))
                s += "\n" + TEMPLATE_END;

            vm = new();
            using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(s));
            var instructions = ByteToOpcodeDecompiler.Parse(stream);
            object result = vm.Execute(instructions, clearInternals: false);
            if(result is not TemplateGroup template)
                throw new InvalidOperationException("SkipWhen template did not evaluate to a TemplateGroup");
            this.value = template;
        }
        else
            this.value = value;

        bool test = ShouldSkip(false);
    }

    internal bool ShouldSkip(object? actual)
    {
        if(value is TemplateGroup group && group.TryCall(out object? returnVal, [actual], vm))
        {
            if(returnVal is bool b)
                return b;
            throw new InvalidOperationException("SkipWhen template did not return a boolean value");
        }
        else
        {
            if (actual == null && value == null)
                return true;
            if (actual != null && actual.Equals(value))
                return true;
            return false;
        }
    }
}
