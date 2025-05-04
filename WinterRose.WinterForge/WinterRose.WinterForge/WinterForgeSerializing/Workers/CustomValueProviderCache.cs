using System;
using System.Collections.Generic;

namespace WinterRose.WinterForgeSerializing.Workers;

internal static class CustomValueProviderCache
{
    private static readonly Dictionary<Type, CustomValueProviderINTERNAL> valueProviders = [];

    static CustomValueProviderCache()
    {
        var serializers = TypeWorker.FindTypesWithBase<CustomValueProviderINTERNAL>();
        foreach (Type serializer in serializers)
        {
            if (serializer.Name is "CustomValueProvider`1" or "CustomValueProviderINTERNAL")
                continue; // skip base types

            CustomValueProviderINTERNAL instance = (CustomValueProviderINTERNAL)Activator.CreateInstance(serializer);
            if (instance != null)
                valueProviders.Add(instance.Type, instance);
        }
    }

    public static bool Get(Type t, out CustomValueProviderINTERNAL provider)
    {
        if(t.Name is "Nullable`1")
        {
            // type is nullable, find the type for its generic type
            t = t.GetGenericArguments()[0];
            return valueProviders.TryGetValue(t, out provider!);
        }
        return valueProviders.TryGetValue(t, out provider!);
    }
}
