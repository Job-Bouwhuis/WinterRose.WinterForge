using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using WinterRose.AnonymousTypes;

namespace WinterRose.Reflection
{
    public static class Util
    {
        public static bool IsAnonymousType(this Type type)
        {
            if (type.Name.Contains("<>f__AnonymousType"))
                return true;
            return type.GetCustomAttribute<AnonymousAttribute>() is not null;
        }
    }
}
