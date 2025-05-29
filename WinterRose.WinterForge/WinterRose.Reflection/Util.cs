using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinterRose.Reflection
{
    public static class Util
    {
        public static bool IsAnonymousType(this Type type)
        {
            return type.Name.Contains("<>f__AnonymousType");
        }
    }
}
