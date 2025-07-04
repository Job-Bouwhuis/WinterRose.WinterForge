namespace WinterRose.Reflection
{
    internal sealed class ListToHashSet<T>
        : TypeConverter<List<T>, HashSet<T>>
    {
        public override HashSet<T> Convert(List<T> source) => [.. source];
    }

    internal sealed class HashSetToList<T>
        : TypeConverter<HashSet<T>, List<T>>
    {
        public override List<T> Convert(HashSet<T> source) => [.. source];
    }
}
