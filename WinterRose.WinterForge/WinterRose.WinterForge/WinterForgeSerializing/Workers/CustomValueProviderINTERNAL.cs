using System;

namespace WinterRose.WinterForgeSerializing.Workers
{
    /// <summary>
    /// Used internally to browse types to find custom value providers
    /// </summary>
    public abstract class CustomValueProviderINTERNAL
    {
        internal abstract Type Type { get; }
        internal abstract string _CreateString(object? obj, ObjectSerializer serializer);
        internal abstract object? _CreateObject(string value, InstructionExecutor executor);
    }
}
