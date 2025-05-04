using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinterRose.WinterForgeSerializing.Workers
{
    internal class OverridableStack<T> : IEnumerable<T>
    {
        private readonly List<T> items = new();

        public int Count => items.Count;

        public void PushEnd(T item)
        {
            items.Add(item);
        }

        public void PushStart(T item)
        {
            items.Insert(0, item);
        }

        public T Pop()
        {
            if (items.Count == 0)
                throw new InvalidOperationException("Queue is empty.");

            T value = items[0];
            items.RemoveAt(0);
            return value;
        }

        public T Peek()
        {
            if (items.Count == 0)
                throw new InvalidOperationException("Queue is empty.");

            return items[0];
        }

        public T PeekLast()
        {
            if (items.Count == 0)
                throw new InvalidOperationException("Queue is empty.");

            return items[^1];
        }

        public void Clear()
        {
            items.Clear();
        }

        public void OverrideAt(int index, T item)
        {
            if (index < 0 || index > items.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            if (index == items.Count)
                items.Add(item);
            else
                items[index] = item;
        }

        public IEnumerator<T> GetEnumerator() => items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    }

}
