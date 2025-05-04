using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinterRose.WinterForgeSerializing
{
    /// <summary>
    /// <see cref="InstructionExecutor"/> has no specific object to return. Can be passed as the deserialize generic argument to inform the deserializer its okey to return <see cref="Nothing"/>
    /// </summary>
    public class Nothing
    {
        /// <summary>
        /// The collection of all the objects that were deserialized
        /// </summary>
        public Dictionary<int, object> AllDeserializedObjects { get; }

        internal Nothing(Dictionary<int, object> allobjects) => AllDeserializedObjects = allobjects;
    }
}
