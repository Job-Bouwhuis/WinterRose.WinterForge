
# Anonymous type syntax

Anonymous types are a funky thing, why would you even want to serialize and deserialize them
ill be honest barely anyone may actually use it in a actual project rather than just to test it out.

Regardless, ive had a blast working on them

#### Other docs
[CSharp Usage](CSharp_Usage.md)  
[Syntax Features](Syntax_Features.md)  
[Built-in Functions](WinterForge_Built-in_Functions.md)  
[Custom Value Providers](CustomValueProvider_Examples.md)  

Define an anonymous type:
```
Anonymous : 0 {
	// fields have to have type specified here. because theres no existing C# class backing this data representation.
	
	int:X = 5;
	string:Name - "my Name";
}
```

Anonymous types can be given a name

```
Anonymous as MyAnonymous : 0 {
// same field definition as specified above
}
```

Anoymous types can be given a custom base class

```
Anonymous as MyAnonymous inherits MyBassClass : 0 {

}
```

Hold up? base class? names?
yes lol.
WinterForge calls upon the anonymous type generation found in WinterRose.Reflection.
it generates a new type using IL generation with the given property name and types.
optionally given a custom name, and base type

When no custom base type is given, its base type will be 'Anonymous' and the 'this\[string]' indexer will be overriden
That indexer will based on the string passed, return the variable of that name.
for example, you specified the anonymous type should have a property `int:X = 5`
it would return X, not through reflection, but through generated IL that gets all the JIT benefits.
This indexer is *not* generated when a custom base class is given.



i want to set properties and fields on the base class, but also wantto add custom properties on the generated anonymous type... sure!
```
Anonymous as MyAnonymous inherits MyBaseClass : 0 {
	myBaseField = 5;
	int:myCustomProperty = 10;
}
```
By specifying the type, it will create a new property for that field, by *not* specifying the type, 
it will assume the property exists on the base class.
this syntax only works when a custom base class was provided.