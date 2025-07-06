namespace WinterRose.Reflection
{
    /// <summary>
    /// Base interface for <see cref="TypeConverter{TSource, TTarget}"/>
    /// </summary>
    public interface ITypeConverter
    {
        Type SourceType { get; }
        Type TargetType { get; }
        object Convert(object source);
    }
}
