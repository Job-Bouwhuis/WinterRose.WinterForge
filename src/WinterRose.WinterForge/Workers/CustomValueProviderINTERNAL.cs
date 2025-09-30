using System;
using System.Reflection.Metadata.Ecma335;

namespace WinterRose.WinterForgeSerializing.Workers
{
    /// <summary>
    /// Used internally to browse types to find custom value providers
    /// </summary>
    public abstract class CustomValueProviderINTERNAL
    {
        internal abstract Type Type { get; }
        internal abstract object _CreateString(object? obj, ObjectSerializer serializer);
        internal abstract object? _CreateObject(object value, WinterForgeVM executor);
        /// <summary>
        /// If the value from the serialized data represents null, this method is called.
        /// </summary>
        /// <returns>by default, null</returns>
        internal virtual object? OnNull() => null;
    }
}
