using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using WinterRose.Reflection;

namespace WinterRose;

public static class StructUtils
{
    private static ConcurrentDictionary<Type, bool> cache= new();

    extension(Marshal)
    {
        public static byte[] PackStruct<T>(T str) where T : struct
        {
            if (new ReflectionHelper(typeof(T)).GetMembers().ContainsRefTypes())
                throw new InvalidOperationException($"Struct {typeof(T).Name} contains reference type fields, which cannot be packed.");

            int size = Marshal.SizeOf<T>();
            byte[] bytes = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(str, ptr, true);
                Marshal.Copy(ptr, bytes, 0, size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            return bytes;
        }

        public static T UnpackStruct<T>(byte[] bytes) where T : struct
        {
            if(!cache.TryGetValue(typeof(T), out var containsRefs))
            {
                containsRefs = new ReflectionHelper(typeof(T)).GetMembers().ContainsRefTypes();
                cache.TryAdd(typeof(T), containsRefs);
            }

            if (!containsRefs)
                throw new InvalidOperationException($"Struct {typeof(T).Name} contains reference type fields, which cannot be unpacked.");

            int size = Marshal.SizeOf<T>();
            if (bytes.Length != size)
                throw new ArgumentException($"Byte array size does not match struct size ({size} bytes).");

            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.Copy(bytes, 0, ptr, size);
                return Marshal.PtrToStructure<T>(ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
    }

    extension(List<MemberData> list)
    {
        private bool ContainsRefTypes()
        {
            foreach (MemberData member in list)
                if (!member.Type.IsValueType)
                    return false;
            return true;
        }
    }
}
