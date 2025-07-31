using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinterRose.WinterForgeSerializing.Workers;

namespace WinterRose.WinterForgeSerializing
{
    /// <summary>
    /// Winterforge invokes the method right before the instance is going to be serialized
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class BeforeSerializeAttribute() : FlowHookAttribute;

    /// <summary>
    /// Winterforge invokes the method right before the instance is going to be deserialzied (at this point the instance is created, but no fields have been touched yet)
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class BeforeDeserializeAttribute() : FlowHookAttribute;

    /// <summary>
    /// Winterforge invokes the method right after deserialization has happened for this specific instance
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class AfterDeserializeAttribute() : FlowHookAttribute;
}

namespace WinterRose.WinterForgeSerializing.Workers
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class FlowHookAttribute : Attribute
    {
        /// <summary>
        /// When the method returns a <see cref="Task"/>, this property indicates whether the task is awaited or not.
        /// </summary>
        public bool IsAwaited { get; set; } = false;
    }
}
