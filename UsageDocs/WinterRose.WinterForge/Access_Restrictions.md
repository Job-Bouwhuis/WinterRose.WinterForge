# Restricting access to fields/properties/methods from within the WinterForge format
WinterForge allows calling methods on static types and instances previously or in the process of being deserialized.
This can be a security risk. thats why this access limiting feature exists!

#### NOTE:
These filters and the global restrictions operate ONLY on the accessing syntax such as "Foo->bar" or "Foo->Qar()"  
During standard deserialization there is no governing which fields can have their value set.  
Without accessing, theres no way to read a variable  
  

## Examples:
```cs
public class FileManager
{
    public string RootDirectory { get; init; }

    public FileManager(string root) => RootDirectory = root;

    public string ReadTextFile(string relativePath) =>
        File.ReadAllText(Path.Combine(RootDirectory, relativePath));
}
```


Now we wouldnt want the ability to read files and stuff.
By default WinterForge blocks a bunch of types that could result in dangerous attacks
such as the file system, threads, and at first use it scans all. yes all types 
for methods that implement [DllImport] or [LibraryImport]
This can be toggled by `AccessFilterCache.BlockPInvoke = false;`

to block a type that currently isnt blocked, you can do
```cs
AccessFilter filter = AccessFilterCache.GetFilter(typeof(FileManager), AccessFilterKind.Whitelist);
// its set to whitelist, so everything *not* defined in the filter is blocked.

// say we want to allow only the RootDirectory property. wed do
filter.Govern(nameof(FileManager.RootDirectory));

// now only that property is allowed, and all others are blocked.
```

WinterForge example:
``` 
FileManager : 0;
alias 0 as fm

// this is allowed
fm->RootDirectory = "c:\";

Anonymous : 1 {
    // the method call will result in a 'WinterForgeAccessIllegalException' 
    string:fileReadAttack = fm->ReadTextFile("sensitiveFile.txt");
}

// someone could even want to go as far as to imediately want to connect a TCP
// everything TCP and UDP is blocked by default

// note that creating the TcpClient instance is allowed
// accessing any members on it is not
TcpClient("127.0.0.1", 8000) : 3;
alias 3 as tcp

// throws 'WinterForgeAccessIllegalException'
tcp->Send(_ref(1)->fileReadAttack);
```


## Globalized restrictions
`WinterForge.GlobalAccessRestriction = WinterForgeGlobalAccessRestriction.AllAccessing;`  
having the options of these values:  
#### `AllAccessing`
- **All access is blocked**, including any syntax using `->`.
- Serializing statics is **not possible** with this setting.

#### `InstanceOnly`
- **All access on static types is blocked**.
- Serializing statics is **not possible** with this setting.

#### `InstanceVariablesOnly`
- **All access to static members and all methods (static + instance) is blocked**.
- Only instance variables are allowed.
- Serializing statics is **not possible** with this setting.

#### `StaticAndInstanceVariableOnly`
- **All method access is blocked**, both static and instance.
- **Static and instance variables are allowed**.
- Serializing statics **is possible** with this setting.

#### `NoGlobalBlock`
- **No global rule applied**.
- All access decisions are delegated to the configured `AccessFilter`.