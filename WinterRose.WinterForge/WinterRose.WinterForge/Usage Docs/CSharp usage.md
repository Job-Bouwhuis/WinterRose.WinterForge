
# How to use WinterForge in C#

For convenience, all major functionality has been packed into a generalized static class 'WinterForge' it has methods to 
serialize to various formats, and deserialize from those formats. aswell as to convert various input sources containing human 
readable to the optimized format.

## Serialization

```cs
WinterForge.SerializeToFile(object, string, TargetFormat, WinterForgeProgressTracker)
WinterForge.SerializeToString(object, TargetFormat, WinterForgeProgressTracker)
WinterForge.SerializeToStream(object, Stream, TargetFormat, WinterForgeProgressTracker)

WinterForge.SerializeStaticToFile(Type, string, TargetFormat, WinterForgeProgressTracker)
WinterForge.SerializeStaticToString(Type, TargetFormat, WinterForgeProgressTracker)
WinterForge.SerializeStaticToStream(Type, Stream, TargetFormat, WinterForgeProgressTracker)
```

## Deserialization

```cs
WinterForge.DeserializeFromStream(Stream, WinterForgeProgressTracker)
WinterForge.DeserializeFromStream<T>(Stream, WinterForgeProgressTracker)

WinterForge.DeserializeFromString(string)
WinterForge.DeserializeFromString<T>(string)

WinterForge.DeserializeFromHumanReadableString(string, WinterForgeProgressTracker)
WinterForge.DeserializeFromHumanReadableString<T>(string, WinterForgeProgressTracker)

WinterForge.DeserializeFromHumanReadableStream(Stream, WinterForgeProgressTracker)
WinterForge.DeserializeFromHumanReadableStream<T>(Stream, WinterForgeProgressTracker)

WinterForge.DeserializeFromHumanReadableFile(string, WinterForgeProgressTracker)
WinterForge.DeserializeFromHumanReadableFile<T>(string, WinterForgeProgressTracker)
```

## Format conversion
```cs
WinterForge.ConvertHumanReadable(Stream, Stream)

WinterForge.ConvertFromFileToFile(string, string)
WinterForge.ConvertFromStringToString(string)
WinterForge.ConvertFromFileToString(string)
WinterForge.ConvertFromStringToFile(string, string)

WinterForge.ConvertFromFileToStream(string)
WinterForge.ConvertFromStringToStream(string)
```