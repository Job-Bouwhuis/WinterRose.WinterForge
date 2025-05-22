# WinterForge

WinterForge is a high-performance, human-readable object serialization framework built for environments where speed and developer clarity matter.
Using the two sided format. one for humans, one for the computer, WinterForge achieves high performant serialization cycles while remaining to be readable for a developer.
The system can also immediately go from the intermediate representation for that little extra speed.

## Features

### High Performance Serialization and Deserialization
- Optimized to handle thousands of types and objects in milliseconds.

### Dual-Sided Format
- Human-readable format for debugging, and version control.
- Opcode-based intermediate representation encodes object data using structured, sequential opcodes.
- Supports direct serialization from and deserialization to the opcode format for maximum performance.
- Unlike binary blobs, the opcode format remains readable and traceable, enabling debugging, diffing, and manual editing.

### Smart Type Discovery
- Value Provider system allows for providing a custom value for a given field/property type.
 - uses CustomValueProvider\<T> and no manual registering of a value provider is required.
- Supports both structs and classes.

### Precision Field and Member Control
- Per-field customization including:
  - Inclusion/exclusion based on attribute on the given field/property.
- Static and instance member support:
  - ability to read/write fields and properties, and call methods during deserialization.

## Design
- Focused on speed and clarity.
- Structure-first: data is always read in the order it's written.

## WIP Limitations
- Dictionaries are at this point in time not yet supported by WinterForge.

## License
[You can find it here](LICENSE.md)