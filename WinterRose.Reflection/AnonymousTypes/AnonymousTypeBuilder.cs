using System;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace WinterRose.AnonymousTypes;

/// <summary>
/// A class that can create anonymous types.
/// </summary>
public static class AnonymousTypeBuilder
{
    private static Dictionary<int, Type> existingTypes = [];

    /// <summary>
    /// Creates a new anonymous type from the specified properties.
    /// </summary>
    /// <param name="properties"></param>
    /// <param name="baseType">When not null, the generated type has this type as a base. thereby the accessing of members using 'this[string]' is not possible.
    /// However this can be quite useful when creating things like mods or other kinds of extensible features.</param>
    /// <returns></returns>
    public static Type CreateNewAnonymousType(Dictionary<string, object> properties, string? typeName = null, Type? baseType = null)
    {
        if((properties is null || properties.Count == 0) && typeName is null && baseType == null)
        {
            return typeof(Anonymous); // Return base anonymous type if no customization is needed.
            // Dont waste resources creating a new type that would be identical to the base type.
        }

        baseType ??= typeof(Anonymous);
        var hash = AnonymousTypeHash.GetHashCode(properties, typeName, baseType);

        if (existingTypes.TryGetValue(hash.GetHashCode(), out Type? existing))
            return existing;

        var typeBuilder = AnonymousTypeBuilderHelper.CreateTypeBuilder(typeName, baseType);

        var fieldMap = new Dictionary<string, FieldBuilder>();
        foreach (var property in properties)
            fieldMap.Add(property.Key, AnonymousTypeBuilderHelper.CreateProperty(typeBuilder, property.Key, property.Value?.GetType() ?? typeof(object)));

        if(baseType == typeof(Anonymous))
            AnonymousTypeBuilderHelper.CreateIndexer(typeBuilder, fieldMap);

        Type t = typeBuilder.CreateTypeInfo().UnderlyingSystemType;
        existingTypes.Add(hash.GetHashCode(), t);
        return t;
    }

    private static class AnonymousTypeHash
    {
        public static int GetHashCode(Dictionary<string, object> properties, string? typeName, Type? baseType)
        {
            HashCode hash = new HashCode();

            if (typeName is not null)
                hash.Add(typeName);

            if (baseType is not null)
                hash.Add(baseType);

            foreach (var prop in properties)
            {
                hash.Add(prop.Key);
                hash.Add(prop.Value);
            }

            return hash.ToHashCode();
        }
    }
}
