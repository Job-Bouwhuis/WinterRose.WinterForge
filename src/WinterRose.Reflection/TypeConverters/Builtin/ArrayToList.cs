using System;
using System.Collections;
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

    internal sealed class IListToArray<T> : TypeConverter<IList<T>, T[]>
    {
        public override T[] Convert(IList<T> source) => [.. source];
    }

    internal sealed class IListToArray2<T> : TypeConverter<IList, T[]>
    {
        public override T[] Convert(IList source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var result = new T[source.Count];

            for (int index = 0; index < source.Count; index++)
            {
                object item = source[index]!;

                if (item is T castItem)
                {
                    result[index] = castItem;
                    continue;
                }

                try
                {
                    result[index] = TypeConverter.Convert<T>(item);
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
