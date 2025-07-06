using System.Collections.ObjectModel;

namespace WinterRose.Reflection.TypeConverters.Builtin
{
    internal sealed class ListToReadOnlyCollection<T> :
        TypeConverter<List<T>, ReadOnlyCollection<T>>
    {
        public override ReadOnlyCollection<T> Convert(List<T> source) =>
            new ReadOnlyCollection<T>(source);
    }

    internal sealed class ReadOnlyCollectionToList<T> :
        TypeConverter<ReadOnlyCollection<T>, List<T>>
    {
        public override List<T> Convert(ReadOnlyCollection<T> source) =>
            new List<T>(source);
    }

}
