using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinterRose.WinterForgeSerializing.Compiling;
public abstract class CustomValueCompiler<T> : ICustomValueCompiler
{
    // This hash is used as the unique identifier for the compiled form
    public uint CompilerId { get; private set; }
    uint ICustomValueCompiler.CompilerId { get => CompilerId; set => CompilerId = value; }

    protected CustomValueCompiler()
    {
    }

    /// <summary>
    /// Compile the value into a binary format
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    public abstract void Compile(BinaryWriter writer, T value);

    /// <summary>
    /// Decompile the value from binary format
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public abstract T? Decompile(BinaryReader reader);


    void ICustomValueCompiler.Compile(BinaryWriter writer, object value)
    {
        if (value.GetType() != typeof(T))
            throw new InvalidOperationException($"Compiler expects type {typeof(T).FullName} but got {value.GetType().FullName}");
        Compile(writer, (T)value);
    }
    object? ICustomValueCompiler.Decompile(BinaryReader reader) => Decompile(reader);
}

