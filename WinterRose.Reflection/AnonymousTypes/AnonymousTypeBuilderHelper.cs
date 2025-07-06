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

        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("WinterRose.Reflection.GeneratedTypes"), AssemblyBuilderAccess.RunAndCollect);
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

        getIL.Emit(OpCodes.Ldarg_0); // this
        getIL.Emit(OpCodes.Ldarg_1); // string key
        getIL.Emit(OpCodes.Call, baseGet); // base.get_Item(key)
        getIL.Emit(OpCodes.Ret);

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

            setIL.Emit(OpCodes.Ldarg_0); // Load 'this'

            var fieldType = pair.Value.FieldType;
            var underlying = Nullable.GetUnderlyingType(fieldType);

            if (fieldType.IsValueType && underlying != null)
            {
                // It's a Nullable<T>
                var afterNullable = setIL.DefineLabel();

                setIL.Emit(OpCodes.Ldarg_2);                  // load value
                setIL.Emit(OpCodes.Dup);                      // duplicate to check for null
                setIL.Emit(OpCodes.Brtrue_S, afterNullable);  // if not null, go ahead

                // value is null
                setIL.Emit(OpCodes.Pop);                      // remove duplicate null
                setIL.Emit(OpCodes.Initobj, fieldType);       // initialize nullable to default (null)
                setIL.Emit(OpCodes.Ldloca_S, (byte)0);        // push address of local slot 0 (safe default)
                setIL.Emit(OpCodes.Ldobj, fieldType);         // read defaulted struct
                setIL.MarkLabel(afterNullable);

                if (!setIL.ILOffset.Equals(0))
                {
                    // non-zero offset => store result in field
                    setIL.Emit(OpCodes.Unbox_Any, underlying);
                    var ctor = fieldType.GetConstructor(new[] { underlying });
                    setIL.Emit(OpCodes.Newobj, ctor);
                }
            }
            else if (fieldType.IsValueType)
            {
                setIL.Emit(OpCodes.Ldarg_2);
                setIL.Emit(OpCodes.Unbox_Any, fieldType);
            }
            else
            {
                setIL.Emit(OpCodes.Ldarg_2);
                setIL.Emit(OpCodes.Castclass, fieldType);
            }

            setIL.Emit(OpCodes.Stfld, pair.Value);
            setIL.Emit(OpCodes.Ret);
            setIL.MarkLabel(next);
        }

        // Fallback to base.set_Item(key, value)
        setIL.Emit(OpCodes.Ldarg_0); // this
        setIL.Emit(OpCodes.Ldarg_1); // key
        setIL.Emit(OpCodes.Ldarg_2); // value
        setIL.Emit(OpCodes.Call, baseSet);
        setIL.Emit(OpCodes.Ret);

        // Hook into base
        typeBuilder.DefineMethodOverride(getMethod, baseGet);
        typeBuilder.DefineMethodOverride(setMethod, baseSet);  
    }


}