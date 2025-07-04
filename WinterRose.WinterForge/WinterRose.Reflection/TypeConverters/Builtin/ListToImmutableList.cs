using System.Collections.Immutable;

namespace WinterRose.Reflection.TypeConverters.Builtin
{
    // Immutability
    internal sealed class ListToImmutableList<T> :
        TypeConverter<List<T>, ImmutableList<T>>
    {
        public override ImmutableList<T> Convert(List<T> source) =>
            source.ToImmutableList();
    }

    internal sealed class ImmutableListToList<T> :
    TypeConverter<ImmutableList<T>, List<T>>
    {
        public override List<T> Convert(ImmutableList<T> source) =>
            source.ToList();
    }

}
