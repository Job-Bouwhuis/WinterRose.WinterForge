using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinterRose.Reflection
{
    /// <summary>
    /// Wrapper to hold a direct reference to a struct, and allows it to be stored like a class
    /// </summary>
    public unsafe class StructReference
    {
        private readonly void* ptr;
        /// <summary>
        /// Wrapper to hold a direct reference to a struct, and allows it to be stored like a class<br></br><br></br>
        /// 
        /// Use with absolute care.
        /// </summary>
        /// <param name="ptr"></param>
        public StructReference(void* ptr) => this.ptr = ptr;
        /// <summary>
        /// Gets the referenced value
        /// </summary>
        /// <returns></returns>
        public ref object Get() => ref *(object*)ptr;
        /// <summary>
        /// Gets the referenced value as <typeparamref name="T"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public ref T As<T>() => ref *(T*)ptr;
        
    }
}
