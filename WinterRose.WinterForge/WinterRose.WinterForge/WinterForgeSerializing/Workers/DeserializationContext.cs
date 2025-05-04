using System;
using System.Collections.Generic;
using WinterRose.Reflection;

namespace WinterRose.WinterForgeSerializing.Workers
{
    internal class DeserializationContext : IDisposable
    {
        internal Dictionary<int, object> ObjectTable { get; } = [];
        internal Stack<object> ValueStack { get; } = new();
        internal List<DeferredObject> DeferredObjects { get; } = [];
        public bool IsDisposed { get; private set; }

        internal void AddObject(int id, ref object instance)
        {
            ObjectTable.Add(id, instance);
        }

        public void Dispose()
        {
            ObjectTable.Clear();
            ValueStack.Clear();
            DeferredObjects.Clear();
        }

        internal object? GetObject(int id)
        {
            ObjectTable.TryGetValue(id, out var obj);
            return obj;
        }

        internal void MoveStackTo(int id)
        {
            object o = ValueStack.Pop();
            if (o is StructReference sr)
                ObjectTable[id] = sr.Get();
            else
                ObjectTable[id] = o;
        }
    }

}
