# Stream-Based Object Serialization and Deserialization WinterForge

WinterForge is a flexible and human-friendly object 
serialization system that works around your project, not the other way around. 
It's not as blazing fast as barebones formats like JSON, but for everything it does, 
it's honestly pretty quick. You get the best of both worlds: a clean and readable text 
format for devs, plus a compact opcode-based format

It's tuned for the kind of power it gives you, especially 
when you're dealing with complex object setups or need to handle versioning gracefully. 
So yeah, maybe not the fastest out there, but definitely one of the most capable.

The projects within this repository are licensed under the MPL-2.0 license

**Slight disclaimer**
I work on this package alone and on my free time. updates may be slow.
Contact me on discord `thesnowowl` if you want to get into contact with me. 
be it in interest in usage of WinterForge, contribution, or other packages made by me,
all folks are welcome!

#### **Find Usage docs here!**
- [CSharp Usage](UsageDocs/WinterRose.WinterForge/CSharp_Usage.md)  
- [Syntax Features](UsageDocs/WinterRose.WinterForge/Syntax_Features.md)  
- [Anonymous Type Syntax](UsageDocs/WinterRose.WinterForge/Anonymous_Type_Syntax.md)  
- [Built-in Functions](UsageDocs/WinterRose.WinterForge/WinterForge_Built-in_Functions.md)  
- [Custom Value Providers](UsageDocs/WinterRose.WinterForge/CustomValueProvider_Examples.md)  
- [Custom Value Compilers](UsageDocs/WinterRose.WinterForge/CustomValueCompiler.md)
- [Flow Hooks](UsageDocs/WinterRose.WinterForge/FlowHooks.md)  
- [Access Restrictions](UsageDocs/WinterRose.WinterForge/Access_Restrictions.md)  
- [Scripting](UsageDocs/WinterRose.WinterForge/Scripting.md)

[Find quick benchmarks here!](#quick-performance-overviews)
[Find quick start and syntax examples here!](#quick-examples)

## **Core Features**

- **Stream-based I/O:**  
  - Utilizes `IO.Stream` for serialization and deserialization, enabling flexible storage and transfer.

- **Dictionaries with Arbitrary Keys and Values:**  
  - Supports `Dictionary<TKey, TValue>` with **any** object as `TKey` or `TValue`.  
  - Nested dictionaries, lists, and combinations are fully supported.  

- **Dual Format System:**  
  - **Human-readable text:** Easy for developers to read, debug, diff, and edit manually.  
  - **Opcode intermediate representation:** Structured sequential opcodes for fast, optimized serialization cycles.

- **Comprehensive Type Support:**  
  - Primitive types (`int`, `float`, `bool`, `string`, etc.) with full typename transcription. (eg: int is recognized as System.Int32) 
  - **Anonymous types:** Supports serialization and deserialization of inline, unnamed objects, including nested anonymous types.  
  - Nested objects, enums, lists, arrays, and nullable types.  
  - Static classes, fields, and properties.

- **Attribute-Driven Control:**  
  - Inclusion/exclusion of fields and properties using attributes.  
  - Hooks on instance methods for lifecycle events:  
    - `BeforeSerialize`  
    - `BeforeDeserialize`  
    - `AfterDeserialize`  
  - Hooks can be asynchronous (`async Task`). by default they are fired and forgotten, but optionally they can be awaited before the serialization or deserialization process continues.

- **Advanced Object Handling:**  
  - Object reference ID system with aliasing and stack-based referencing for reuse.  
  - Ability to call methods during deserialization, using return values dynamically.  
  - Custom value providers via `CustomValueProvider<T>` for type-specific value control, without manual registration. [More info here!](UsageDocs/WinterRose.WinterForge/CustomValueProvider_Examples.md)  
  - Supports both structs and classes.

- **Progress and Formatting:**  
  - Abstract progress tracking system (useful for loading bars, UI feedback).  
  - Formatting modes controlled by `TargetFormat` enum:  
    - `HumanReadable`  
    - `IndentedHumanReadable`  
    - `Opcodes`  
  - Automatic conversion between human-readable and opcode formats.

- **Smart Type Discovery and Reflection:**  
  - Dynamically discovers types and members.  
  - Supports runtime variables and integration with reflection helpers.

## **Design Philosophy**

- **Performance + Developer Clarity:**  
  Optimized to serialize thousands of objects in milliseconds while maintaining human readability for easier debugging and version control.

- **Structure-First Approach:**  
  Data is always read in the order it is written, ensuring deterministic and reliable serialization.

## **Current Limitations & Future Plans**
- Upcoming features:  
  - Support for math and boolean expressions within serialized data.  
  - Importing and including other WinterForge files/modules.  
  - Templates and repeatable code blocks.  
  - Conditional serialization and expression support.  


## **Quick performance Overviews**

| CPU             | Serialization (ms)           | Deserialization (ms)       | RAM Used (KB) |
|-----------------|------------------------------|----------------------------|---------------|
| Ryzen 9 9950X3D | Best: 8785 / Worst: 11533    | Best: 2466 / Worst: 2793   | Min: 1,126.28 / Max: 1,557.30 |
| Intel i7 650H   | Best: 13,033 / Worst: 14,866 | Best: 6,068 / Worst: 6,503 | Min: 1,128.87 / Max: 1,542.54 KB |
| Submit your     | own CPU stats                | to my discord, or a        | pull request |

  ```
  ------WinterForge Inspection------
  Total instructions: 2513093
  Raw bytes: 82458265 B
  Compressed bytes: 4261271 B
  Compression ratio: 0,052
  
  Instruction histogram:
    SET: 921600
    ELEMENT: 354290
    LIST_START: 309401
    LIST_END: 309401
    DEFINE: 309200
    END: 309200
    RET: 1
  ---------------end---------------
  ```

## **Quick Examples**
Using these types as an example
```csharp
public enum Gender
{
    Male,
    Female,
    Other
}

public class Person
{
    public string name;
    public int age;
    public bool isBald;
    public Gender gender;
    public List<Person> children;
    public Person mother;

    public static Person NewPerson()
    {
        Person p = new();
        p.name = "Roza";
        p.age = 21;
        p.isBald = false;
        p.gender = Gender.Female;

        Person kid = new();
        kid.name = "Riven";
        kid.age = 5;
        kid.isBald = false;
        kid.gender = Gender.Male;
        kid.mother = p;

        p.children = [kid];

        return p;
    }
}
```

Serialized using:
```cs
Person foo = Person.NewPerson();
// Serialized using
WinterForge.SerializeToFile(foo, "foo.txt");

// Deserialized using 
Person fooClone = WinterForge.DeserializeFromFile<Person>("foo.txt");
```
#### **Human Readable**
```text
Person : 0 {
    name = "Roza";
    age = 21;
    isBald = false;
    gender = Gender.Female;
    mother = null;
    children = <Person>[
        Person : 1 {
            name = "Riven";
            age = 5;
            isBald = false;
            gender = Gender.Male;
            children = null;
            mother = #ref(0);
        }
    ]
}
```

#### **Opcodes**
Opcode format may change in the future. Backwards compatibility between this format and the potential new one will exist if the format ever changes
```text
0 Person 0 0
1 name "Roza"
1 age 21
1 isBald false
1 gender 1
1 mother null
6 Person
0 Person 1 0
1 name "Riven"
1 age 5
1 isBald false
1 gender 0
1 children null
1 mother #ref(0)
2 1
5 #ref(1)
7
1 children #stack()
2 0
```

#### **Binary Opcodes**
WinterForge compiles the IR opcodes as shown above into a bytestream optimized for the combination of speed, and data size.


## Default Field/Property Inclusion Rules
  Public fields and auto-properties are included by default.
  Private fields and properties are excluded unless explicitly included.
  Static members are excluded by default but can be included with [WFInclude].
  Backing fields can be explicitly included using [field: WFInclude].
  Properties with custom logic are excluded by default unless opted-in.
  To exclude any field or property included by default, apply [WFExclude] attribute to them
  Fields or properties that are not writable are skipped and can in no way be included

## **License**
This project is licensed under the terms described [here](LICENSE.md).

if markdown link references dont work, please read the docs on the [Github page then](https://github.com/Job-Bouwhuis/WinterRose.WinterForge)