using System.Reflection.Emit;
using System.Reflection;
using System;

namespace WinterRose.AnonymousTypes;

/// <summary>
/// A class that creates the underlying type for an anonymous type.
/// </summary>
public static class AnonymousTypeBuilderHelper
{
    /// <summary>
    /// Creates a new type builder.
    /// </summary>
    /// <returns></returns>
    public static TypeBuilder CreateTypeBuilder(string? typeName = null, Type? baseType = null)
    {
        typeName ??= $"AnonymousType_{Guid.NewGuid()}";
        baseType ??= typeof(Anonymous);

        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("WinterRose.Reflection.GeneratedTypes"), AssemblyBuilderAccess.Run);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule("GeneratedTypes");
        var typeBuilder = moduleBuilder.DefineType(typeName, TypeAttributes.Public);
        typeBuilder.SetParent(baseType);

        Type attr = typeof(AnonymousAttribute);
        CustomAttributeBuilder attrBuilder = new(attr.GetConstructors().First(), []);
        typeBuilder.SetCustomAttribute(attrBuilder);
        return typeBuilder;
    }

    /// <summary>
    /// Creates a new property for the specified type buidler.
    /// </summary>
    /// <param name="typeBuilder"></param>
    /// <param name="propertyName"></param>
    /// <param name="propertyType"></param>
    public static FieldBuilder CreateProperty(TypeBuilder typeBuilder, string propertyName, Type propertyType)
    {
        var fieldBuilder = typeBuilder.DefineField($"<{propertyName}>k__BackingField", propertyType, FieldAttributes.Private);
        var propertyBuilder = typeBuilder.DefineProperty(propertyName, PropertyAttributes.HasDefault, propertyType, null);

        // getter
        var getterMethodBuilder = typeBuilder.DefineMethod($"get_{propertyName}",
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            propertyType, Type.EmptyTypes);

        var getterIL = getterMethodBuilder.GetILGenerator();
        getterIL.Emit(OpCodes.Ldarg_0);
        getterIL.Emit(OpCodes.Ldfld, fieldBuilder);
        getterIL.Emit(OpCodes.Ret);

        // setter
        var setterMethodBuilder = typeBuilder.DefineMethod($"set_{propertyName}",
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            null, [propertyType]);

        var setterIL = setterMethodBuilder.GetILGenerator();
        setterIL.Emit(OpCodes.Ldarg_0);
        setterIL.Emit(OpCodes.Ldarg_1);
        setterIL.Emit(OpCodes.Stfld, fieldBuilder);
        setterIL.Emit(OpCodes.Ret);

        propertyBuilder.SetGetMethod(getterMethodBuilder);
        propertyBuilder.SetSetMethod(setterMethodBuilder);

        return fieldBuilder;
    }

    public static void CreateIndexer(TypeBuilder typeBuilder, Dictionary<string, FieldBuilder> fields)
    {
        var baseGet = typeof(Anonymous).GetMethod("get_Item");
        var baseSet = typeof(Anonymous).GetMethod("set_Item");

        // Define getter method
        var getMethod = typeBuilder.DefineMethod("get_Item",
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
            typeof(object), new[] { typeof(string) });

        var getIL = getMethod.GetILGenerator();
        var endGet = getIL.DefineLabel();

        foreach (var pair in fields)
        {
            var next = getIL.DefineLabel();
            getIL.Emit(OpCodes.Ldarg_1); // load string arg
            getIL.Emit(OpCodes.Ldstr, pair.Key);
            getIL.Emit(OpCodes.Call, typeof(string).GetMethod("Equals", new[] { typeof(string), typeof(string) }));
            getIL.Emit(OpCodes.Brfalse_S, next);

            getIL.Emit(OpCodes.Ldarg_0);
            getIL.Emit(OpCodes.Ldfld, pair.Value);
            if (pair.Value.FieldType.IsValueType)
                getIL.Emit(OpCodes.Box, pair.Value.FieldType);
            getIL.Emit(OpCodes.Ret);
            getIL.MarkLabel(next);
        }

        getIL.Emit(OpCodes.Ldstr, "Property not found");
        getIL.Emit(OpCodes.Newobj, typeof(Exception).GetConstructor(new[] { typeof(string) }));
        getIL.Emit(OpCodes.Throw);

        // Define setter method
        var setMethod = typeBuilder.DefineMethod("set_Item",
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
            null, new[] { typeof(string), typeof(object) });

        var setIL = setMethod.GetILGenerator();

        foreach (var pair in fields)
        {
            var next = setIL.DefineLabel();
            setIL.Emit(OpCodes.Ldarg_1);
            setIL.Emit(OpCodes.Ldstr, pair.Key);
            setIL.Emit(OpCodes.Call, typeof(string).GetMethod("Equals", new[] { typeof(string), typeof(string) }));
            setIL.Emit(OpCodes.Brfalse_S, next);

            setIL.Emit(OpCodes.Ldarg_0);
            setIL.Emit(OpCodes.Ldarg_2);
            if (pair.Value.FieldType.IsValueType)
                setIL.Emit(OpCodes.Unbox_Any, pair.Value.FieldType);
            else
                setIL.Emit(OpCodes.Castclass, pair.Value.FieldType);
            setIL.Emit(OpCodes.Stfld, pair.Value);
            setIL.Emit(OpCodes.Ret);
            setIL.MarkLabel(next);
        }

        setIL.Emit(OpCodes.Ldstr, "Property not found");
        setIL.Emit(OpCodes.Newobj, typeof(Exception).GetConstructor(new[] { typeof(string) }));
        setIL.Emit(OpCodes.Throw);

        // Hook into base
        typeBuilder.DefineMethodOverride(getMethod, baseGet);
        typeBuilder.DefineMethodOverride(setMethod, baseSet);
    }


}