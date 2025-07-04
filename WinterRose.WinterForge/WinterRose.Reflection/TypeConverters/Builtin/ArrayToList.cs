using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinterRose.Reflection.TypeConverters.Builtin
{
    internal sealed class ArrayToList<T> : TypeConverter<T[], List<T>>
    {
        public override List<T> Convert(T[] source) => [.. source];
    }

    internal sealed class ListToArray<T> : TypeConverter<List<T>, T[]>
    {
        public override T[] Convert(List<T> source) => [.. source];
    }

}
