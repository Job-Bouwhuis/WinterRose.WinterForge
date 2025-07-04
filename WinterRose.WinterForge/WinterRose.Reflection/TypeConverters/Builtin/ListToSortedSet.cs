namespace WinterRose.Reflection.TypeConverters.Builtin
{
    // Ordering & uniqueness
    internal sealed class ListToSortedSet<T> :
        TypeConverter<List<T>, SortedSet<T>>
    {
        public override SortedSet<T> Convert(List<T> source) =>
            new SortedSet<T>(source);
    }

    internal sealed class SortedSetToList<T> :
    TypeConverter<SortedSet<T>, List<T>>
    {
        public override List<T> Convert(SortedSet<T> source) =>
            new List<T>(source);
    }

}
