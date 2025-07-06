using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinterRose.Reflection
{
    /// <summary>
    /// Gets thrown when a type is not found when using the serializer
    /// </summary>
    [Serializable]
    public class TypeNotFoundException : Exception
    {
        public TypeNotFoundException() { }
        public TypeNotFoundException(string message) : base(message) { }
        public TypeNotFoundException(string message, Exception inner) : base(message, inner) { }
    }
}
