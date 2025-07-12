# Struct reference
Holds the pointer to a struct in a unsafe way. They require the pointer be passed in, therefor can only be created in a unsafe context

Usage:
```cs

Vector2 vec = new Vector2(1, 2);
StructRefernce sr = new StructReference(&vec);

Vector2 sameVec = sr.Get<Vector2>();
```