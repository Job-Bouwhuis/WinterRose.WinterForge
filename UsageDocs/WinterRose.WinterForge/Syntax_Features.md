# Winterforge, a very flexible serializer which is still fast for its broad featureset

#### Other docs
- [CSharp Usage](CSharp_Usage.md)  
- [Anonymous Type Syntax](Anonymous_Type_Syntax.md)  
- [Built-in Functions](WinterForge_Built-in_Functions.md)  
- [Custom Value Providers](CustomValueProvider_Examples.md)   
- [CustomValueCompiler](CustomValueCompiler.md)
- [Flow Hooks](FlowHooks.md)  
- [Access Restrictions](Access_Restrictions.md)
- [Scripting](Scripting.md)

## Creating instances
```
// there are several ways to define an instance
Vector2 : 0 {
// here you can set variables of the created type, for example X and Y
}

Vector2 : 0;
// this defines the type using its default empty constructor and does not change any of its variables

Vector2(1, 2) : 0;
// this defines the type using the constructor that matches the arguments given, and does not change any of its variables.

Vector2(1, 2) : 0 {
// here it creates the type using the constructor that matches the arguments given, and you can further alter the variables here
}
```

## List/Array collections
Collections can be defined as top level, meaning the serializer returns the collection, or assigned to a field
#### General syntax
```
<collectiontype>[
element1,
element2,
element3
]
```
Elements can be on the same line, or the next line. the comma that seperates the elements can also be on its own seperate line such as:
```
<collectiontype>[
element1
,
element2
,
element3
]```

#### int collection as top level
```
<int>[1, 2, 3, 4, 5]
return _stack();
```

#### int collection as a field
```
nums = <int>[1, 2, 3, 4, 5]
```

## Dictionary collections

```
<keytype, valuetype>[
 key1 => value1,
 key2 => value2
]
```

eg:
```
<int, string>[
	1 => "one",
	2 => "two"
]
```
assigning and top level function the same as lists, just with this syntax


## Static / Instance accessing

Accessing variables and methods from a static type, or an already created instance.
It was created with statics in mind, but support for instances was merely chaning a few lines, so it was added too



```
// instance
Vector2(1, 2) : 0;

_ref(0)->X = 5;

// static
AppSettings->Vsync = true;
```

Accessing can also be chained
```
AppSettings->Window->Vsync = true;
```

Method calls are like you are probably imagining given the accessing syntax above, except methods may never be on the left of the = sign. currently (as of 25.2.19) there is no support to just call a method and ignore the result. this is planned in the future.

```
myWork = Worker->ComputeSomething();
```


## Aliasing

writing WinterForge manually, and tired of always having to type _ref() everywhere?
introducing aliasing.

```
Vector2 : 0;
alias 0 as vec;
```

the alias 'vec' can  now be used everywhere where you would use the \_ref() call


## Returning values

The serializer doesnt by default know what specific object to return to the caller. It has to be told what to return.

```
Vector2(1, 2) : 0;
return 0;
```
returns object at reference id 0 to the caller. (as of 25.2.18 an alias can not be used here. its planned for a later version)

'return' does allow _stack() as return parameter in case a collection is required to be returned.


## Enums
Enums are defined like this:
```
Foo = Bar.Baz;
```
where 'Foo' is the name of the field, and 'Bar' is the enum type, and 'Baz' is the enum value.
in C# enums can have the [Flags] attribute, which allows for bitwise operations on the enum values.
In WinterForge, multiple enum values can be combined using the | operator, like this:
```
Foo = Bar.Baz | Qux;
```
You dont repeat the enum type, just the values. Its illegal to do for example `Foo = Bar.Baz | Bar.Qux`. WinterForge expects only the values to be after |

## Comments
Comments are defined like this:
```
// this is a comment
```
Its like that in both the human readable syntax, and the opcode syntax, however i dont know why you would want to write comments in the opcode syntax, as its not human readable anyway.
it was added for the "// Parsed by WinterForge {version}" line.

## Multi line strings
```
myString = """""
This is my multuline
string. you can also put up to 4 consecutive quotes in here """"
"""""
```