namespace WinterRose.Reflection.TypeConverters.Builtin
{
    // Interface wrappers
    internal sealed class ArrayToIReadOnlyList<T> :
        TypeConverter<T[], IReadOnlyList<T>>
    {
        public override IReadOnlyList<T> Convert(T[] source) =>
            Array.AsReadOnly(source);
    }

    internal sealed class IReadOnlyListToArray<T> : TypeConverter<IReadOnlyList<T>, T[]>
    {
        public override T[] Convert(IReadOnlyList<T> source)
        {
            if (source is T[] array)
                return array;

            var result = new T[source.Count];
            for (int i = 0; i < source.Count; i++)
                result[i] = source[i];
            return result;
        }
    }

}
