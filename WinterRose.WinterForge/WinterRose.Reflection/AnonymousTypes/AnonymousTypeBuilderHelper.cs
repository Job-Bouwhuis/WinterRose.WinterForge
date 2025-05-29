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
    public static TypeBuilder CreateTypeBuilder(string typeName)
    {
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("DynamicAssembly"), AssemblyBuilderAccess.Run);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule("DynamicModule");
        var typeBuilder = moduleBuilder.DefineType(typeName, TypeAttributes.Public);

        typeBuilder.SetParent(typeof(Anonymous));

        return typeBuilder;
    }

    /// <summary>
    /// Creates a new property for the specified type buidler.
    /// </summary>
    /// <param name="typeBuilder"></param>
    /// <param name="propertyName"></param>
    /// <param name="propertyType"></param>
    public static void CreateProperty(TypeBuilder typeBuilder, string propertyName, Type propertyType)
    {
        var fieldBuilder = typeBuilder.DefineField($"<{propertyName}>k__BackingField", propertyType, FieldAttributes.Private);
        var propertyBuilder = typeBuilder.DefineProperty(propertyName, PropertyAttributes.HasDefault, propertyType, null);

        // Create getter
        var getterMethodBuilder = typeBuilder.DefineMethod($"get_{propertyName}", 
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, 
            propertyType, 
            Type.EmptyTypes);

        var getterIL = getterMethodBuilder.GetILGenerator();
        getterIL.Emit(OpCodes.Ldarg_0);
        getterIL.Emit(OpCodes.Ldfld, fieldBuilder);
        getterIL.Emit(OpCodes.Ret);

        // Create setter
        var setterMethodBuilder = typeBuilder.DefineMethod($"set_{propertyName}", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, null, [propertyType]);
        var setterIL = setterMethodBuilder.GetILGenerator();
        setterIL.Emit(OpCodes.Ldarg_0);
        setterIL.Emit(OpCodes.Ldarg_1);
        setterIL.Emit(OpCodes.Stfld, fieldBuilder);
        setterIL.Emit(OpCodes.Ret);

        propertyBuilder.SetGetMethod(getterMethodBuilder);
        propertyBuilder.SetSetMethod(setterMethodBuilder);
    }
}