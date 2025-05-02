# WinterForge

WinterForge is a high-performance, human-readable object serialization framework built for environments where speed and developer clarity matter.
Using the two sided format. one for humans, one for the computer, WinterForge achieves high preformant serialization cycles while remaining to be readable for a developer.
The system can also immediately go from the intermediate representation for that little extra speed 

## Key Features

- **Opcode-Based Serialization Format**  
  A custom intermediate representation that encodes object data using structured, sequential opcodes. Designed to be parsed fast and still interpretable by a human.

- **Human-Readable Format**  
  Unlike binary blobs, WinterForge's opcode format is readable and traceable, enabling debugging, diffing, and manual editing when necessary.

- **Static and Instance Member Support** *(Planned)*  
  Future support for calling static and instance methods, as well as reading and writing fields and properties during deserialization, will enable behavior-aware loading.

- **Value Provider System**  
  Types are interpreted using registered value providers. If a type exists in the loaded assemblies and is not a nested type, it is auto-discovered and used without needing manual registration.

## Design Philosophy

- Focused on speed and clarity  
- Structure-first: data is always read in the order it's written  
- Minimal runtime overhead  
- Modular and extensible, with pluggable behaviors

## Limitations

- Not meant to replace general-purpose serializers (like JSON/XML)
- Threaded deserialization is currently experimental due to sequential nature
- Requires discipline in how data is written and read to maintain deterministic output

## Status

WinterForge is under development and currently private. APIs and format structure are subject to change as features are finalized.

## License
[You can find it here](LICENSE.md)