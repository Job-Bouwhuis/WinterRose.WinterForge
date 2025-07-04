using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinterRose.Reflection.TypeConverters.Builtin
{
    internal class IListToList<T> : TypeConverter<IList<T>, List<T>>
    {
        public override List<T> Convert(IList<T> source)
        {
            if (source is List<T> l)
                return l;
            return [.. source];
        }
    }

    internal class ListToIList<T> : TypeConverter<List<T>, IList<T>>
    {
        public override IList<T> Convert(List<T> source) => source;
    }
}
