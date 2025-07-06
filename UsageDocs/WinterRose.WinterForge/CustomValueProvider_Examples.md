# CustomValueProvidder
The abstract class to give WinterForge some context on how to serialize the value.
rather than doing it per-field, its able to be given a single value.

#### NOTE:
This value should *not* contain the space character, or it should be wrapped in quotes (as such: "your value")
On the 'GetObject' method (more on that method below) you do not have to account for these quotes, they are removed automatically

#### Other docs
[CSharp Usage](CSharp_Usage.md)  
[Syntax Features](Syntax_Features.md)  
[Anonymous Type Syntax](Anonymous_Type_Syntax.md)  
[Built-in Functions](WinterForge_Built-in_Functions.md)  
[Flow Hooks](FlowHooks.md)  

## Code examples
```cs
class MyValueProvider : CustomValueProvider<Foo>
{
    public override string CreateString(Foo obj, ObjectSerializer serializer)
    {
        return $"\"{obj.Name}\"";
    }

    public override Foo CreateObject(string value, InstructionExecutor executor)
    {
        return Foo.FromName(value);
    }
}

```