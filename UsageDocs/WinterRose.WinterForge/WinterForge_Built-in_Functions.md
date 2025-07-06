# Built-in functions within the WinterForge format

#### Other docs
[CSharp Usage](CSharp_Usage.md)  
[Syntax Features](Syntax_Features.md)  
[Anonymous Type Syntax](Anonymous_Type_Syntax.md)  
[Custom Value Providers](CustomValueProvider_Examples.md)  
[Flow Hooks](FlowHooks.md)  

## \_ref(int)

Gets a reference to the object with the given integer ID.
In most places within the format, it is allowed that the requested ID is not yet available. 
eg:
```
Transform : 0 {
	Position = _ref(1);
}

Vector3(1, 2, 3) : 1;
```


## \_stack()

Gets and pops the top element on the stack. Used for collections.

eg:
```
<int>[1, 2, 3]
return _stack();
```


## \_type()
Represents a type literal

eg:
```
someType = _type(System.Math);
```