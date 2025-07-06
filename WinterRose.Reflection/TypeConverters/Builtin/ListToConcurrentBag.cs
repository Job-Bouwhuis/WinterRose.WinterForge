using System.Collections.Concurrent;

namespace WinterRose.Reflection.TypeConverters.Builtin
{
    // Concurrency
    internal sealed class ListToConcurrentBag<T> :
        TypeConverter<List<T>, ConcurrentBag<T>>
    {
        public override ConcurrentBag<T> Convert(List<T> source) =>
            new ConcurrentBag<T>(source);
    }

    internal sealed class ConcurrentBagToList<T> :
    TypeConverter<ConcurrentBag<T>, List<T>>
    {
        public override List<T> Convert(ConcurrentBag<T> source) =>
            new List<T>(source);
    }

}
