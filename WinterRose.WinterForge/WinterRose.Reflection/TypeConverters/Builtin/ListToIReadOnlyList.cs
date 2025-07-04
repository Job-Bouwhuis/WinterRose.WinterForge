namespace WinterRose.Reflection.TypeConverters.Builtin
{
    internal sealed class ListToIReadOnlyList<T> :
        TypeConverter<List<T>, IReadOnlyList<T>>
    {
        public override IReadOnlyList<T> Convert(List<T> source) => source;
    }

    internal sealed class IReadOnlyListToList<T> :
    TypeConverter<IReadOnlyList<T>, List<T>>
    {
        public override List<T> Convert(IReadOnlyList<T> source) =>
            source is List<T> list ? list : new List<T>(source);
    }

}
