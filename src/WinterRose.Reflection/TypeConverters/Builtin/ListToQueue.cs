namespace WinterRose.Reflection.TypeConverters.Builtin
{
    internal sealed class ListToQueue<T> :
        TypeConverter<List<T>, Queue<T>>
    {
        public override Queue<T> Convert(List<T> source) =>
            new Queue<T>(source);
    }

    internal sealed class QueueToList<T> :
    TypeConverter<Queue<T>, List<T>>
    {
        public override List<T> Convert(Queue<T> source) =>
            new List<T>(source);
    }

}
