# WinterForge Scripting

WinterForge scripting is an optional extension of the serialization system that allows dynamic behavior, templates, conditional logic, and container-level variables inside your serialized data. By default, **scripting is disabled** — only static data can be serialized and deserialized. This ensures maximum safety and predictability.


#### Other docs
- [CSharp Usage](CSharp_Usage.md)
- [Built-in Functions](WinterForge_Built-in_Functions.md)
- [Syntax Features](Syntax_Features.md)
- [Anonymous Type Syntax](Anonymous_Type_Syntax.md)
- [Custom Value Providers](CustomValueProvider_Examples.md)
- [CustomValueCompiler](CustomValueCompiler.md)
- [Flow Hooks](FlowHooks.md)
- [Access Restrictions](Access_Restrictions.md)
- [Scripting](Scripting.md)


You can enable scripting selectively by setting the `WinterForge.AllowedScriptingLevel` property. The available levels are:

```csharp
public enum ScriptingLevel
{
    None,        // default, no scripting allowed
    Conditions,  // allows if statements and basic control flow, but container or template definitions remain disabled
    All          // full scripting enabled, including containers, templates, and container-level variables
}
```

Example: enabling conditions-level scripting:

```csharp
WinterForge.AllowedScriptingLevel = ScriptingLevel.Conditions;
```

---

## Quick Overview

- **Scripting is optional**: you can serialize and deserialize objects normally without ever enabling scripting.  
- **Data-only variables** are always allowed and safe to use.  
- **Container-level variables, templates, and container definitions** require `ScriptingLevel.All`.  
- **Attempting to execute a script without the proper level** will throw a `WinterForgeExecutionException`.

---

## Example Script

```csharp
for var i = 0; i < 10; i = i + 1;
{
    Console->WriteLine(i);
}

while true
{
    Console->WriteLine("infinite loooooooooooooooop");
}

#template TopLevelTemplate
{

}

#container Foo
{
    variables
    {
        x;
        y = 5; // variables can have constant values as default
    }

    Foo // constructor
    {
    }
    
    Foo int y // constructor overload
    {
        this->y = y;
    }
    
    #template ThisIsAFunction
    {
    }

    #template ThisOneHasArgs int num, string a, Template callback
    {
        // templates can be passed as arguments and stored in variables
        
        // templates and containers can be nested
        #template inner
        {
        }

        #container InnerContainer
        {
            #template templateSeption
            {
            }
        }

        // order of definition and usage doesn't matter
        InnerContainer : ic; // create instance of nested container
        return ic; // return instance
    }
}
```

> This example demonstrates loops, container and template definitions, variables, nesting, and how templates can be used as first-class objects. All of these features are only permitted when the appropriate `ScriptingLevel` is enabled.
