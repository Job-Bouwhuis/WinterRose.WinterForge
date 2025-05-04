using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using System.Runtime.CompilerServices;

namespace WinterRose.WinterForgeSerializing.Workers;

public static class ListExtensions
{
    static class ArrayAccessor<T>
    {
        public static Func<List<T>, T[]> Getter;

        static ArrayAccessor()
        {
            var dm = new DynamicMethod("get", MethodAttributes.Static | MethodAttributes.Public, CallingConventions.Standard, typeof(T[]), new Type[] { typeof(List<T>) }, typeof(ArrayAccessor<T>), true);
            var il = dm.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, typeof(List<T>).GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance));
            il.Emit(OpCodes.Ret);
            Getter = (Func<List<T>, T[]>)dm.CreateDelegate(typeof(Func<List<T>, T[]>));
        }
    }

    private static readonly MethodInfo genericMethod;

    static ListExtensions()
    {
        genericMethod = typeof(ListExtensions)
            .GetMethods(BindingFlags.Static | BindingFlags.Public)
            .FirstOrDefault(m =>
                m.Name == "GetInternalArray" &&
                m.IsGenericMethodDefinition &&
                m.GetGenericArguments().Length == 1 &&
                m.GetParameters().Length == 1 &&
                m.GetParameters()[0].ParameterType.IsGenericType &&
                m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(List<>)
            )!;
    }

    public static T[] GetInternalArray<T>(this List<T> list)
    {
        T[] items = ArrayAccessor<T>.Getter(list);
        int count = list.Count;

        if (count == items.Length)
            return items;

        // Unavoidable copy here if Capacity > Count
        T[] trimmed = new T[count];
        Array.Copy(items, 0, trimmed, 0, count);
        return trimmed;
    }

    public static object[] GetInternalArray(this IList list)
    {
        Type t = list.GetType();
        if(t.Name == "List`1")
        {
            Type param = t.GetGenericArguments()[0];
            object result = genericMethod.MakeGenericMethod(param).Invoke(null, [list]);
            return Unsafe.As<object[]>(result);
        }
        throw new Exception("invalid list type");
    }
}
