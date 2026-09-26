using WinterRose.WinterForgeSerializing;

namespace WinterRose;

/// <summary>
/// Specifies that the field or properties is to not be serialized by <see cref="WinterForge"/>
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public class WFExcludeAttribute : Attribute
{
}