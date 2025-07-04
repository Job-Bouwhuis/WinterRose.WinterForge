using System.Collections.ObjectModel;

namespace WinterRose.Reflection.TypeConverters.Builtin
{
    internal sealed class DictionaryToReadOnlyDictionary<TKey, TValue> :
        TypeConverter<Dictionary<TKey, TValue>, ReadOnlyDictionary<TKey, TValue>>
        where TKey : notnull
    {
        public override ReadOnlyDictionary<TKey, TValue> Convert(Dictionary<TKey, TValue> source) =>
            new ReadOnlyDictionary<TKey, TValue>(source);
    }

    internal sealed class ReadOnlyDictionaryToDictionary<TKey, TValue> :
    TypeConverter<ReadOnlyDictionary<TKey, TValue>, Dictionary<TKey, TValue>>
    where TKey : notnull
    {
        public override Dictionary<TKey, TValue> Convert(ReadOnlyDictionary<TKey, TValue> source) =>
            new Dictionary<TKey, TValue>(source);
    }

}
