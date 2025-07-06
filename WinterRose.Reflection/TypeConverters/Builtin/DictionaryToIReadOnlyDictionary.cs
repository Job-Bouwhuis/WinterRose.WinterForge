using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinterRose.Reflection.TypeConverters.Builtin
{
    internal sealed class DictionaryToIReadOnlyDictionary<TKey, TValue> :
        TypeConverter<Dictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>>
        where TKey : notnull
    {
        public override IReadOnlyDictionary<TKey, TValue> Convert(Dictionary<TKey, TValue> source) =>
            new ReadOnlyDictionary<TKey, TValue>(source);
    }

    internal sealed class IReadOnlyDictionaryToDictionary<TKey, TValue> :
    TypeConverter<IReadOnlyDictionary<TKey, TValue>, Dictionary<TKey, TValue>>
    where TKey : notnull
    {
        public override Dictionary<TKey, TValue> Convert(IReadOnlyDictionary<TKey, TValue> source) =>
            new Dictionary<TKey, TValue>(source);
    }

}
