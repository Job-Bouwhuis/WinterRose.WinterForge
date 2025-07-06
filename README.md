# Stream-Based Object Serialization and Deserialization — WinterForge

WinterForge is a high-performance, human-readable object serialization 
framework designed to adapt fluidly to project needs rather than forcing projects 
to conform to its rules. It blends speed with developer clarity by offering a dual 
format system that supports both human-readable text and opcode-based intermediate representation 
for maximum performance.

**Slight disclaimer**
I work on this package alone and on my free time. updates may be slow.
Contact me on discord 'thesnowowl' if you want to get into contact with me. be it in interest in usage of WinterForge, contribution, or other packages made by me, all folks are welcome!

**Find Usage docs here!** 
[CSharp Usage](UsageDocs/WinterRose.WinterForge/CSharp_Usage.md)  
[Syntax Features](UsageDocs/WinterRose.WinterForge/Syntax_Features.md)  
[Anonymous Type Syntax](UsageDocs/WinterRose.WinterForge/Anonymous_Type_Syntax.md)  
[Built-in Functions](UsageDocs/WinterRose.WinterForge/WinterForge_Built-in_Functions.md)  

## Core Features

- **Stream-based I/O:**  
  - Utilizes `IO.Stream` for serialization and deserialization, enabling flexible storage and transfer.

- **Dictionaries with Arbitrary Keys and Values:**  
  - Supports `Dictionary<TKey, TValue>` with **any** object as `TKey` or `TValue`.  
  - Nested dictionaries, lists, and combinations are fully supported.  

- **Dual Format System:**  
  - **Human-readable text:** Easy for developers to read, debug, diff, and edit manually.  
  - **Opcode intermediate representation:** Structured sequential opcodes for fast, optimized serialization cycles.

- **Comprehensive Type Support:**  
  - Primitive types (`int`, `float`, `bool`, `string`, etc.) with full typename transcription.  
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
  - Custom value providers via `CustomValueProvider<T>` for type-specific value control, without manual registration.  
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

## Design Philosophy

- **Performance + Developer Clarity:**  
  Optimized to serialize thousands of objects in milliseconds while maintaining human readability for easier debugging and version control.

- **Structure-First Approach:**  
  Data is always read in the order it is written, ensuring deterministic and reliable serialization.

## Current Limitations & Future Plans
- Upcoming features:  
  - Support for math and boolean expressions within serialized data.  
  - Importing and including other WinterForge files/modules.  
  - Templates and repeatable code blocks.  
  - Conditional serialization and expression support.  

## License

You can find the license details [here](LICENSE.md).
