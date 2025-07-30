using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinterRose.WinterForgeSerializing.Compiling;

public static class CustomValueCompilerRegistry
{
    private static readonly Dictionary<Type, ICustomValueCompiler> _compilersByType = new();
    private static readonly Dictionary<uint, ICustomValueCompiler> _compilersById = new();

    static CustomValueCompilerRegistry()
    {
        var compilerTypes = TypeWorker.FindTypesWithBase(typeof(CustomValueCompiler<>));
        foreach (var compilerType in compilerTypes)
        {
            // Extract the generic argument (T)
            var baseType = compilerType.BaseType;
            while (baseType != null && (!baseType.IsGenericType || baseType.GetGenericTypeDefinition() != typeof(CustomValueCompiler<>)))
            {
                baseType = baseType.BaseType;
            }
            if (baseType == null) continue;

            var targetType = baseType.GetGenericArguments()[0];

            // Create instance of the compiler
            ICustomValueCompiler instance = (ICustomValueCompiler)Activator.CreateInstance(compilerType);

            if (instance == null) continue;

            // Compute hash from target type's full name + assembly name
            uint id = ComputeHash(targetType);
            instance.CompilerId = id;

            _compilersByType[targetType] = instance;
            _compilersById[id] = instance;
        }
    }

    public static bool TryGetByType(Type type, [NotNullWhen(true)] out ICustomValueCompiler? compiler)
    {
        return _compilersByType.TryGetValue(type, out compiler);
    }

    public static bool TryGetById(uint id, out ICustomValueCompiler? compiler)
    {
        return _compilersById.TryGetValue(id, out compiler);
    }

    private static uint ComputeHash(Type type)
    {
        string fqName = type.FullName + ", " + type.Assembly.FullName;
        return GenerateId(fqName);
    }

    private static uint GenerateId(string input)
    {
        // Just a simple FNV-1a 32-bit hash to get a stable uint ID
        unchecked
        {
            const uint FNV_OFFSET = 2166136261;
            const uint FNV_PRIME = 16777619;

            uint hash = FNV_OFFSET;
            foreach (char c in input)
                hash = (hash ^ c) * FNV_PRIME;

            return hash;
        }
    }
}

