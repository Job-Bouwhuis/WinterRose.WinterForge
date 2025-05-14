using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinterRose.WinterForgeSerializing.Workers;

namespace WinterRose.WinterForgeSerializing
{
    /// <summary>
    /// <see cref="InstructionExecutor"/> has no specific object to return. 
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
