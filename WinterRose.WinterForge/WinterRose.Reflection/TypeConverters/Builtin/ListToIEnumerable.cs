using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinterRose.Reflection.TypeConverters.Builtin
{
    internal class ListToIEnumerable<T> : TypeConverter<List<T>, IEnumerable<T>>
    {
        public override IEnumerable<T> Convert(List<T> source) => source;
    }

    internal class IEnumerableToList<T> : TypeConverter<IEnumerable<T>, List<T>>
    {
        public override List<T> Convert(IEnumerable<T> source)
        {
            if (source is List<T> t)
                return t;

            return [.. source];
        }
    }
}
