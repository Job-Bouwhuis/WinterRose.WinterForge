using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace WinterRose.WinterForgeSerializing;

public class ObjectPool<T> : IDisposable where T : class, new()
{
    private readonly ConcurrentBag<(T Item, DateTime LastUsed)> pool = new();
    private readonly Timer cleanupTimer;
    private readonly TimeSpan idleTime;
    private int count = 0;
    private readonly int maxSize;
    private readonly Action<T>? resetAction;

    public class PoolExhaustedException : Exception
    {
        public PoolExhaustedException() : base("Object pool has been exhausted.") { }
    }

    public class PoolDisposedException : Exception
    {
        public PoolDisposedException() : base("Object pool has been disposed.") { }
    }

    public class PoolResetException : Exception
    {
        public PoolResetException() : base("Object pool reset action failed.") { }
    }

    public struct ItemRental : IDisposable
    {
        private readonly ObjectPool<T> pool;
        public T Item { get; }
        public ItemRental(ObjectPool<T> pool, T item)
        {
            this.pool = pool;
            Item = item;
        }
        public void Dispose()
        {
            pool.Return(Item);
        }

        public static implicit operator T(ItemRental rental) => rental.Item;
    }

    public ObjectPool(int initialSize = 10, int maxSize = 100, TimeSpan? idleTime = null, Action<T>? resetAction = null)
    {
        this.maxSize = maxSize;
        this.idleTime = idleTime ?? TimeSpan.FromMinutes(5);
        this.resetAction = resetAction;

        for (int i = 0; i < initialSize; i++)
        {
            pool.Add((new T(), DateTime.UtcNow));
            count++;
        }

        cleanupTimer = new Timer(CleanupIdleInstances, null, this.idleTime, this.idleTime);
    }

    public T Rent()
    {
        while (pool.TryTake(out var item))
        {
            return item.Item;
        }

        if (count < maxSize)
        {
            Interlocked.Increment(ref count);
            return new T();
        }

        throw new InvalidOperationException("Pool exhausted");
    }

    public ItemRental Using() => new ItemRental(this, Rent());

    public void Return(T item)
    {
        // Reset the instance if a reset action was provided
        resetAction?.Invoke(item);

        pool.Add((item, DateTime.UtcNow));
    }

    private void CleanupIdleInstances(object? state)
    {
        List<(T, DateTime)> remaining = new();
        while (pool.TryTake(out var item))
        {
            if (DateTime.UtcNow - item.LastUsed < idleTime)
            {
                remaining.Add(item);
            }
            else
            {
                Interlocked.Decrement(ref count);
                if (item.Item is IDisposable disposable)
                    disposable.Dispose();
            }
        }

        foreach (var item in remaining)
            pool.Add(item);
    }

    public void Dispose()
    {
        cleanupTimer.Dispose();

        while (pool.TryTake(out var item))
        {
            if (item.Item is IDisposable disposable)
                disposable.Dispose();
        }
    }
}
