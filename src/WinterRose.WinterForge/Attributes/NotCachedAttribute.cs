using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinterRose
{
    /// <summary>
    /// Notifies <see cref="WinterRose.WinterForgeSerializing.WinterForge"/> not to cache this object duing serialization
    /// </summary>
    /// <remarks>
    /// NOTE: this may lead to a stack overflow if a type using this 
    /// attribute is part of a circle reference
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = true)]
    class NotCachedAttribute : Attribute
    {
    }
}
