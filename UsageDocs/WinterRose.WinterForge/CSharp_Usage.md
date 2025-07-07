# How to use WinterForge in C#

For convenience, all major functionality has been packed into a generalized static class 'WinterForge' it has methods to 
serialize to various formats, and deserialize from those formats. aswell as to convert various input sources containing human 
readable to the optimized format.

#### Other docs
- [Syntax Features](Syntax_Features.md)  
- [Anonymous Type Syntax](Anonymous_Type_Syntax.md)  
- [Built-in Functions](WinterForge_Built-in_Functions.md)  
- [Custom Value Providers](CustomValueProvider_Examples.md)  
- [Flow Hooks](FlowHooks.md)  
- [Access Restrictions](Access_Restrictions.md)  

## Serialization

#### Synchrounous
```cs
WinterForge.SerializeToFile(object, string, TargetFormat, WinterForgeProgressTracker)
WinterForge.SerializeToString(object, TargetFormat, WinterForgeProgressTracker)
WinterForge.SerializeToStream(object, Stream, TargetFormat, WinterForgeProgressTracker)

WinterForge.SerializeStaticToFile(Type, string, TargetFormat, WinterForgeProgressTracker)
WinterForge.SerializeStaticToString(Type, TargetFormat, WinterForgeProgressTracker)
WinterForge.SerializeStaticToStream(Type, Stream, TargetFormat, WinterForgeProgressTracker)

// example:
var foo = new Foo(420, "some data");
WinterForge.SerializeToFile(foo, "foo.txt");
```

#### Asynchronous
```cs
SerializeToFileAsync(object, string, TargetFormat, WinterForgeProgressTracker)
SerializeToStringAsync(object, TargetFormat, WinterForgeProgressTracker)
SerializeToStreamAsync(object, Stream, TargetFormat, WinterForgeProgressTracker)

SerializeStaticToFileAsync(Type, string, TargetFormat, WinterForgeProgressTracker)
SerializeStaticToStringAsync(Type, TargetFormat, WinterForgeProgressTracker)
SerializeStaticToStreamAsync(Type, Stream, TargetFormat, WinterForgeProgressTracker)

// example:
var foo = new Foo(420, "some data");
WinterForgeSerializationTask serTask = WinterForge.SerializeToFileAsync(foo, "foo.txt");
await serTask;
```

## Deserialization

#### Synchrounous
```cs
WinterForge.DeserializeFromStream(Stream, WinterForgeProgressTracker)
WinterForge.DeserializeFromStream<T>(Stream, WinterForgeProgressTracker)

WinterForge.DeserializeFromString(string)
WinterForge.DeserializeFromString<T>(string)

WinterForge.DeserializeFromFile(string, WinterForgeProgressTracker)
WinterForge.DeserializeFromFile<T>(string, WinterForgeProgressTracker)

WinterForge.DeserializeFromFileAsync(string, WinterForgeProgressTracker)
WinterForge.DeserializeFromFileAsync<T>(string, WinterForgeProgressTracker)

WinterForge.DeserializeFromHumanReadableString(string, WinterForgeProgressTracker)
WinterForge.DeserializeFromHumanReadableString<T>(string, WinterForgeProgressTracker)

WinterForge.DeserializeFromHumanReadableStream(Stream, WinterForgeProgressTracker)
WinterForge.DeserializeFromHumanReadableStream<T>(Stream, WinterForgeProgressTracker)

WinterForge.DeserializeFromHumanReadableFile(string, WinterForgeProgressTracker)
WinterForge.DeserializeFromHumanReadableFile<T>(string, WinterForgeProgressTracker)

// example:
Foo foo = WinterForge.DeserializeFromFile<Foo>("foo.txt");
```

#### Asynchronous
```cs
WinterForge.DeserializeFromStreamAsync(Stream, WinterForgeProgressTracker)
WinterForge.DeserializeFromStreamAsync<T>(Stream, WinterForgeProgressTracker)

WinterForge.DeserializeFromStringAsync(string, Encoding, WinterForgeProgressTracker)
WinterForge.DeserializeFromStringAsync<T>(string, Encoding, WinterForgeProgressTracker)

WinterForge.DeserializeFromFileAsync(string, WinterForgeProgressTracker)
WinterForge.DeserializeFromFileAsync<T>(string, WinterForgeProgressTracker)

WinterForge.DeserializeFromHumanReadableStringAsync(string, WinterForgeProgressTracker)
WinterForge.DeserializeFromHumanReadableStringAsync<T>(string, WinterForgeProgressTracker)

WinterForge.DeserializeFromHumanReadableStreamAsync(Stream humanReadable, WinterForgeProgressTracker)
WinterForge.DeserializeFromHumanReadableStreamAsync<T>(Stream humanReadable, WinterForgeProgressTracker)

WinterForge.DeserializeFromHumanReadableFileAsync(string, WinterForgeProgressTracker)
WinterForge.DeserializeFromHumanReadableFileAsync<T>(string, WinterForgeProgressTracker)

// example:
WinterForgeDeserializationTask deserTask = WinterForge.DeserializeFromFile<Foo>("foo.txt);
Foo f = await deserTask; 

// or skip creating the variable for the deserTask and await the deserialize method directly
f = await WinterForge.DeserializeFromFile<Foo>("foo.txt");
```

## Format conversion. 
WinterForge only supports human readable > opcodes. it does not support opcodes > human readable  
```cs
WinterForge.ConvertFromStreamToString(Stream, Stream)

WinterForge.ConvertFromFileToFile(string, string)
WinterForge.ConvertFromStringToString(string)
WinterForge.ConvertFromFileToString(string)
WinterForge.ConvertFromStringToFile(string, string)

WinterForge.ConvertFromFileToStream(string)
WinterForge.ConvertFromStringToStream(string)

// example:
WinterForge.ConvertFromFileToFile("foo.txt");
```