using System.Collections.Immutable;

namespace WinterRose.Reflection.TypeConverters.Builtin
{
    internal sealed class DictionaryToImmutableDictionary<TKey, TValue> :
        TypeConverter<Dictionary<TKey, TValue>, ImmutableDictionary<TKey, TValue>>
        where TKey : notnull
    {
        public override ImmutableDictionary<TKey, TValue> Convert(Dictionary<TKey, TValue> source) =>
            source.ToImmutableDictionary(pair => pair.Key, pair => pair.Value);
    }

    internal sealed class ImmutableDictionaryToDictionary<TKey, TValue> :
    TypeConverter<ImmutableDictionary<TKey, TValue>, Dictionary<TKey, TValue>>
    where TKey : notnull
    {
        public override Dictionary<TKey, TValue> Convert(ImmutableDictionary<TKey, TValue> source) =>
            new Dictionary<TKey, TValue>(source);
    }

}
