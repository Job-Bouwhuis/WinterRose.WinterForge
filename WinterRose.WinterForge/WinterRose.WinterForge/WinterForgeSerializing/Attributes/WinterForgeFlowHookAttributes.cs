using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinterRose.WinterForgeSerializing
{
    /// <summary>
    /// Winterforge invokes the method right before the instance is going to be serialized
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class BeforeSerializeAttribute() : Attribute;

    /// <summary>
    /// Winterforge invokes the method right before the instance is going to be deserialzied (at this point the instance is created, but no fields have been touched yet)
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class BeforeDeserializeAttribute() : Attribute;

    /// <summary>
    /// Winterforge invokes the method right after deserialization has happened for this specific instance
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class AfterDeserializeAttribute() : Attribute;
}
