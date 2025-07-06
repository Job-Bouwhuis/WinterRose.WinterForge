using System;
using System.Collections;
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

    internal class ListToIList2<T> : TypeConverter<List<T>, IList>
    {
        public override IList Convert(List<T> source) => source;
    }

    internal class IListToList2<T> : TypeConverter<IList, List<T>>
    {
        public override List<T> Convert(IList source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var result = new List<T>(source.Count);

            for (int index = 0; index < source.Count; index++)
            {
                object item = source[index]!;

                if (item is T castItem)
                {
                    result.Add(castItem);
                    continue;
                }

                try
                {
                    result.Add(TypeConverter.Convert<T>(item));
                    continue;
                }
                catch
                {

                }

                throw new InvalidCastException(
                    $"Cannot convert element of type {item.GetType()} at index {index} to {typeof(T)}.");
            }

            return result;
        }
    }
}
