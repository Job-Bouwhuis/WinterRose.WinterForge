using System;
using System.Collections.Generic;

namespace WinterRose.AnonymousTypes;

/// <summary>
/// A class that can create anonymous types.
/// </summary>
public static class AnonymousTypeBuilder
{
    /// <summary>
    /// Creates a new anonymous type from the specified properties.
    /// </summary>
    /// <param name="properties"></param>
    /// <returns></returns>
    public static Type CreateNewAnonymousType(Dictionary<string, object> properties, string? typeName = null)
    {
        typeName ??= $"AnonymousType_{Guid.NewGuid()}";
        var typeBuilder = AnonymousTypeBuilderHelper.CreateTypeBuilder(typeName);

        // Define properties
        foreach (var property in properties)
        {
            AnonymousTypeBuilderHelper.CreateProperty(typeBuilder, property.Key, property.Value.GetType());
        }

        return typeBuilder.CreateTypeInfo().UnderlyingSystemType;
    }
}
