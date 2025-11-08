namespace WinterRose;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property,  AllowMultiple = false, Inherited = true)]
public class ReadOnlyAttribute : Attribute;