# Built-in functions within the WinterForge format

#### Other docs
- [CSharp Usage](CSharp_Usage.md)  
- [Syntax Features](Syntax_Features.md)  
- [Anonymous Type Syntax](Anonymous_Type_Syntax.md)  
- [Custom Value Providers](CustomValueProvider_Examples.md)   
- [CustomValueCompiler](CustomValueCompiler.md)
- [Flow Hooks](FlowHooks.md)  
- [Access Restrictions](Access_Restrictions.md)  

## \#ref(int)

Gets a reference to the object with the given integer ID.
In most places within the format, it is allowed that the requested ID is not yet available. 
eg:
```
Transform : 0 {
	Position = #ref(1);
}

Vector3(1, 2, 3) : 1;
```


## \#stack()

Gets and pops the top element on the stack. Mostly used for collections.

eg:
```
<int>[1, 2, 3]
var numbers = #stack();
```

## \#type()
Represents a type literal

eg:
```
var someType = #type(System.Math);
```
