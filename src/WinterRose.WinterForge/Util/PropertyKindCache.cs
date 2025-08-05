using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace WinterRose.WinterForgeSerializing.Util;

/// <summary>
/// Defines the kind of property any property is identified as 
/// </summary>
public enum PropertyKind
{
    /// <summary>
    /// The kind is unknown
    /// </summary>
    Unknown,
    /// <summary>
    /// The property has custom logic defined
    /// </summary>
    Custom,
    /// <summary>
    /// The property is an auto property.
    /// <br></br>eg: <code>public int MyProp { get; set; }</code>
    /// </summary>
    Auto
}

internal class PropertyKindCache
{
    private static readonly Dictionary<Type, Dictionary<string, PropertyKind>> propertyKindCache = new();

    public static PropertyKind GetPropertyKind(Type type, PropertyInfo property)
    {
        if (!propertyKindCache.TryGetValue(type, out var props))
            propertyKindCache[type] = props = new();

        if (!props.TryGetValue(property.Name, out var kind))
            props[property.Name] = kind = AnalyzeProperty(property);

        return kind;
    }

    private static PropertyKind AnalyzeProperty(PropertyInfo property)
    {
        var getMethod = property.GetGetMethod(nonPublic: true);
        var setMethod = property.GetSetMethod(nonPublic: true);
        if (getMethod == null || setMethod == null) return PropertyKind.Custom;

        var getIL = getMethod.GetMethodBody()?.GetILAsByteArray();
        var setIL = setMethod.GetMethodBody()?.GetILAsByteArray();
        if (getIL == null || setIL == null) return PropertyKind.Custom;

        // IL for: get => field;
        if (getIL.Length != 7 || getIL[0] != 2 || getIL[1] != 123 || getIL[5] != 4 || getIL[6] != 42)
            return PropertyKind.Custom;

        // IL for: set => field = value;
        if (setIL.Length != 8 || setIL[0] != 2 || setIL[1] != 3 || setIL[2] != 125 || getIL[5] != 4 || getIL[6] != 42)
            return PropertyKind.Custom;

        return PropertyKind.Auto;
    }
}
