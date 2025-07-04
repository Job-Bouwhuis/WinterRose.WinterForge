using System.Collections.Immutable;

namespace WinterRose.Reflection.TypeConverters.Builtin
{
    internal sealed class ListToImmutableArray<T> :
        TypeConverter<List<T>, ImmutableArray<T>>
    {
        public override ImmutableArray<T> Convert(List<T> source) =>
            ImmutableArray.CreateRange(source);
    }

    internal sealed class ImmutableArrayToList<T> :
    TypeConverter<ImmutableArray<T>, List<T>>
    {
        public override List<T> Convert(ImmutableArray<T> source) =>
            source.ToList();
    }

}
