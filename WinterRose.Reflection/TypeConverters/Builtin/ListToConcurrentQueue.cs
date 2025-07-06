using System.Collections.Concurrent;

namespace WinterRose.Reflection.TypeConverters.Builtin
{
    internal sealed class ListToConcurrentQueue<T> :
        TypeConverter<List<T>, ConcurrentQueue<T>>
    {
        public override ConcurrentQueue<T> Convert(List<T> source) =>
            new ConcurrentQueue<T>(source);
    }

    internal sealed class ConcurrentQueueToList<T> :
    TypeConverter<ConcurrentQueue<T>, List<T>>
    {
        public override List<T> Convert(ConcurrentQueue<T> source) =>
            new List<T>(source);
    }

}
