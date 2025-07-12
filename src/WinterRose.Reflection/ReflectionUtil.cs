using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using WinterRose.AnonymousTypes;

namespace WinterRose.Reflection
{
    /// <summary>
    /// util extension methods
    /// </summary>
    public static class ReflectionUtil
    {
        /// <summary>
        /// Whether or not the type is anonymous. whether compile time anoymous, any kind of <see cref="Anonymous"/> or generated one alike
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool IsAnonymousType(this Type type)
        {
            if (type.Name.Contains("<>f__AnonymousType"))
                return true;
            if (type.GetCustomAttribute<AnonymousAttribute>() is not null)
                return true;
            return type == typeof(Anonymous);
        }
    }
}
