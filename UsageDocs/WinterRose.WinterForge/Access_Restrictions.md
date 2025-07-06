# Restricting access to fields/properties/methods from within the WinterForge format
WinterForge allows calling methods on static types and instances previously or in the process of being deserialized.
This can be a security risk. thats why this access limiting feature exists!

For example having these classes

```cs
public class Foo 
{
	private string sensitiveString = "Bar";
}

public class FileManager
{
    public string RootDirectory { get; init; }

    public FileManager(string root) => RootDirectory = root;

    public string ReadTextFile(string relativePath) =>
        File.ReadAllText(Path.Combine(RootDirectory, relativePath));

    // Potentially dangerous; easy to blacklist
    public void DeleteEverything() => Directory.Delete(RootDirectory, recursive: true);
}
```


Now we wouldnt want the ability to read files and stuff