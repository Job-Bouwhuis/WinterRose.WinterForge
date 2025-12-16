namespace WinterRose.WinterForgeSerializing.Compiling;

public interface ICustomValueCompiler
{
    internal Type CompilerType { get;}
    internal uint CompilerId { get; set; }
    internal void Compile(BinaryWriter writer, object value);
    internal abstract object? Decompile(BinaryReader reader);
}

