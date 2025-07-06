using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinterRose
{
    using WinterForgeSerializing.Workers;

    /// <summary>
    /// Tells the serializer to serialize this class as the given type, even if the serializer would normally serialize it as a different type
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class SerializeAsAttribute<T> : SerializeAsAttributeINTERNAL
    {
        /// <summary>
        /// The type this class should be serialized as
        /// </summary>
        public override Type Type => typeof(T);
    }
}

namespace WinterRose.WinterForgeSerializing.Workers
{
    public abstract class SerializeAsAttributeINTERNAL : Attribute
    {
        public abstract Type Type { get; }
    }
}
