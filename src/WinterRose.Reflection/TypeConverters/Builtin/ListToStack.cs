namespace WinterRose.Reflection.TypeConverters.Builtin
{
    internal sealed class ListToStack<T> :
        TypeConverter<List<T>, Stack<T>>
    {
        public override Stack<T> Convert(List<T> source) =>
            new Stack<T>(source);
    }

    internal sealed class StackToList<T> :
    TypeConverter<Stack<T>, List<T>>
    {
        public override List<T> Convert(Stack<T> source) =>
            new List<T>(source);
    }

}
