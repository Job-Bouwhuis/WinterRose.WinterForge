using System.Collections.Concurrent;

namespace WinterRose.Reflection
{
    internal sealed class DictionaryToConcurrentDictionary<TKey, TValue>
        : TypeConverter<Dictionary<TKey, TValue>, ConcurrentDictionary<TKey, TValue>>
    {
        public override ConcurrentDictionary<TKey, TValue> Convert(Dictionary<TKey, TValue> source)
            => new(source);
    }

    internal sealed class ConcurrentDictionaryToDictionary<TKey, TValue>
        : TypeConverter<ConcurrentDictionary<TKey, TValue>, Dictionary<TKey, TValue>>
    {
        public override Dictionary<TKey, TValue> Convert(ConcurrentDictionary<TKey, TValue> source) => new(source);
    }
}
