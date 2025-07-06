using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinterRose.WinterForgeSerializing.Workers;

namespace WinterRose.WinterForgeSerializing
{
    /// <summary>
    /// Used to create a custom way to define the way a type is stored using the <see cref="WinterForge"/> serialization system.
    /// </summary>
    public abstract class CustomValueProvider<T> : CustomValueProviderINTERNAL
    {
        internal override Type Type => typeof(T);

        internal override string _CreateString(object? obj, ObjectSerializer serializer)
        {
            return CreateString((T)obj, serializer);
        }

        internal override object? _CreateObject(string value, InstructionExecutor executor)
        {
            return CreateObject(value, executor);
        }

        public abstract string CreateString(T obj, ObjectSerializer serializer);
        public abstract T? CreateObject(string value, InstructionExecutor executor);
    }
}
