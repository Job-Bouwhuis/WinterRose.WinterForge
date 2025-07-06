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
        if (t.Name is "Nullable`1")
        {
            t = t.GetGenericArguments()[0];
        }

        if (valueProviders.TryGetValue(t, out provider!))
            return true;

        foreach (var kvp in valueProviders)
        {
            if (kvp.Key.IsAssignableFrom(t))
            {
                provider = kvp.Value;
                return true;
            }
        }

        provider = null!;
        return false;
    }
}
