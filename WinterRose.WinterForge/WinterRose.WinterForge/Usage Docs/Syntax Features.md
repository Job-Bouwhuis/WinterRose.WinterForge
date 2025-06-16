# Winterforge, a high speed and very flexible serializer

Looking for Anonymous type syntax? head to [Anonymous type syntax](Anonymous type syntax.md)

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
Elements can be on the same line, or the next line. the comma that seperates the elements can also be on its own seperate line

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
**STILL A WORK IN PROGRESS**

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

#### DISCLAIMER
This feature is still being worked on, and isnt in the released package yet (as of 25.2.18)
Once this gets released tho, you can expect keys and value types to be any type you want. be it primitive values, object instances, even other collections.


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

Method calls are like you are probably imagining given the accessing syntax above, except methods may never be on the left of the = sign. currently (as of 25.2.18) there is no support to just call a method and ignore the result. this is planned in the future.

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