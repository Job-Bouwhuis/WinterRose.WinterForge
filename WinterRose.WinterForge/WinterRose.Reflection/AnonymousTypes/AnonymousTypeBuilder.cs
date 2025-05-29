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
    /// <returns></returns>
    public static Type CreateNewAnonymousType(Dictionary<string, object> properties, string? typeName = null)
    {
        var hash = new AnonymousTypeHash(properties);

        if (existingTypes.TryGetValue(hash.GetHashCode(), out Type? existing))
            return existing;

        typeName ??= $"AnonymousType_{Guid.NewGuid()}";
        var typeBuilder = AnonymousTypeBuilderHelper.CreateTypeBuilder(typeName);

        var fieldMap = new Dictionary<string, FieldBuilder>();
        foreach (var property in properties)
            fieldMap.Add(property.Key, AnonymousTypeBuilderHelper.CreateProperty(typeBuilder, property.Key, property.Value.GetType()));

        AnonymousTypeBuilderHelper.CreateIndexer(typeBuilder, fieldMap);

        Type t = typeBuilder.CreateTypeInfo().UnderlyingSystemType;
        existingTypes.Add(hash.GetHashCode(), t);
        return t;
    }

    private class AnonymousTypeHash
    {
        private readonly List<KeyValuePair<string, object>> properties;

        public AnonymousTypeHash(Dictionary<string, object> properties) 
        { 
            this.properties = properties.ToList();
        }

        public override int GetHashCode()
        {
            HashCode hash = new HashCode();

            foreach (var prop in properties)
            {
                hash.Add(prop.Key);
                hash.Add(prop.Value);
            }

            return hash.ToHashCode();
        }

        public override bool Equals(object? obj)
        {
            if (obj is not AnonymousTypeHash other)
                return false;

            for (int i = 0; i < properties.Count; i++)
            {
                if (properties[i].Key != other.properties[i].Key ||
                    properties[i].Value != other.properties[i].Value)
                    return false;
            }

            return true;
        }
    }
}
