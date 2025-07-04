namespace WinterRose.Reflection.TypeConverters.Builtin
{
    internal sealed class DictionaryToSortedDictionary<TKey, TValue> :
        TypeConverter<Dictionary<TKey, TValue>, SortedDictionary<TKey, TValue>>
        where TKey : notnull
    {
        public override SortedDictionary<TKey, TValue> Convert(Dictionary<TKey, TValue> source) =>
            new SortedDictionary<TKey, TValue>(source);
    }

    internal sealed class SortedDictionaryToDictionary<TKey, TValue> :
    TypeConverter<SortedDictionary<TKey, TValue>, Dictionary<TKey, TValue>>
    where TKey : notnull
    {
        public override Dictionary<TKey, TValue> Convert(SortedDictionary<TKey, TValue> source) =>
            new Dictionary<TKey, TValue>(source);
    }

}
