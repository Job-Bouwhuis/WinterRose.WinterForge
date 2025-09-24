# CustomValueCompiler

the compiled variant of [CustomValueProvider](CustomValueProvider_Examples.md)
This system is not foolproof yet i believe, i think the hashes for the types change from PC to PC, but on one pc i believe they are identical
across app runs. further testing will be done and a later version will have a potential fix if this ends up begin a problem



#### Other docs
- [CSharp Usage](CSharp_Usage.md)  
- [Syntax Features](Syntax_Features.md)  
- [Built-in Functions](WinterForge_Built-in_Functions.md)  
- [Custom Value Providers](CustomValueProvider_Examples.md)
- [Anonymous Type Syntax](Anonymous_Type_Syntax.md)  
- [Access Restrictions](Access_Restrictions.md)  
- [Flow Hooks](FlowHooks.md)  


an example of a Vector2 compiler.

```cs
public sealed class Vector2Compiler : CustomValueCompiler<Vector2>
{
    public override void Compile(BinaryWriter writer, Vector2 value)
    {
        writer.Write(value.X);
        writer.Write(value.Y);
    }

    public override Vector2 Decompile(BinaryReader reader)
    {
        float x = reader.ReadSingle();
        float y = reader.ReadSingle();
        return new Vector2(x, y);
    }
}
```