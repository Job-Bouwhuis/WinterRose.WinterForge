# Flow Hooks
A way for an object instance to have a say in what happens before and after its handled

#### Other docs
- [CSharp Usage](CSharp_Usage.md)  
- [Syntax Features](Syntax_Features.md)  
- [Built-in Functions](WinterForge_Built-in_Functions.md)  
- [Custom Value Providers](CustomValueProvider_Examples.md)  
- [Anonymous Type Syntax](Anonymous_Type_Syntax.md)  
- [Access Restrictions](Access_Restrictions.md)  

Done by marking instance methods with one of these attributes  
[BeforeSerialize]  
[BeforeDeserialize]  
[AfterDeserialize]  

These methods are allowed to either return 'void' or 'async Task'

when they return async Task, you can set the optional named parameter in the attribute 'IsAwaited' to true
In that case, WinterForge will wait before continuing the operation. if not, it will call the method and then continue

```cs

public class Foo
{
	public int data;

	[BeforeSerialize]
	private void BeforeSerialize() // visibility modifier doesnt matter
	{
		// some pre-processing before serialize
	}

	// can have more than one in the class hirarchy. for example with derived types
	[BeforeSerialize(IsAwaited = true)]
	private async Task BeforeSerializeAsync()
	{
		// some async pre-processing
	}

	[BeforeDeserialize]
	private void BeforeDeserialize()
	{
		// some pre-processing before the object can receive deserialized data
	}

	[AfterDeserialize]
	private void AfterDeserialize()
	{
		// some post-processing after the entire object has been deserialized.
		// This may be called before any later accessing is done. (with the -> syntax)
	}
}

```