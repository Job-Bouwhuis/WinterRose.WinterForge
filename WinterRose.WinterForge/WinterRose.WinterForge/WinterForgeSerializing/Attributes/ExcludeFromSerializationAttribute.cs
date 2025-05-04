
namespace WinterRose;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public class ExcludeFromSerializationAttribute : Attribute
{
}