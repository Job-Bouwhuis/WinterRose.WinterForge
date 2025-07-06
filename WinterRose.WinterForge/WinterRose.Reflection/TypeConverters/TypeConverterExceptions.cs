
namespace WinterRose.Reflection;

internal static class TypeConverterExceptions
{
    [Serializable]
    internal class CantConvert : Exception
    {
        public CantConvert(Type source, Type target) : base($"Cant convert from '{source.FullName}' to '{target.FullName}'")
        {
        }
    }
}