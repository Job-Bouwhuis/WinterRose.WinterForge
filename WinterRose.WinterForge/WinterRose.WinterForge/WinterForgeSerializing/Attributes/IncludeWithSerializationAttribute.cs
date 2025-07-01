
namespace WinterRose;

/// <summary>
/// Specifies that this property/field should be included when calling any of the serialize methods of <see cref="WinterForgeSerializing.WinterForge"/>
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public class IncludeWithSerializationAttribute : Attribute
{
    /// <summary>
    /// Specifies where this property/field should be in the automatic serialization
    /// </summary>
    public int Priority { get; set; } = 0;
}