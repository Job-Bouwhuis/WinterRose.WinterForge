using System.Runtime.CompilerServices;
using WinterRose.WinterForgeSerializing.Logging;

namespace WinterRose.WinterForgeSerializing;

/// <summary>
/// The base class for <see cref="WinterForgeSerializationTask"/> and <see cref="WinterForgeDeserializationTask{T}"/>
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class WinterForgeAsyncTask<T> : INotifyCompletion
{
    private Action? continuation;
    private Exception? ex;
    private WinterForgeProgressTracker? progress;
    private T? res;
    private Lock threadLock = new();

    /// <summary>
    /// The exception thrown, returns <see langword="null"/> when <code><see cref="IsCompletedSuccessfully"/> = <see langword="true"/></code>and throws the exception when not yet completed/errored
    /// </summary>
    public Exception? Exception
    {
        get => ex ?? (IsCompletedSuccessfully ? null : throw new InvalidOperationException("Task is not yet finished, and hasnt thrown any exceptions yet"));
        internal set
        {
            ex = value;
            continuation?.Invoke();
        }
    }

    /// <summary>
    /// Whether the task was completed
    /// </summary>
    public bool IsCompleted => res != null || ex != null;
    /// <summary>
    /// Whether or not there was an exception thrown
    /// </summary>
    public bool IsCompletedFaulty => ex != null;
    /// <summary>
    /// Whether the task was completed successfully
    /// </summary>
    public bool IsCompletedSuccessfully => res != null;

    /// <summary>
    /// The progress tracker used for this deserialization process
    /// </summary>
    public WinterForgeProgressTracker? ProgressTracker
    {
        get => progress;
        internal set => progress = value;
    }

    /// <summary>
    /// The result. throws the exception when not yet completed
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public T Result
    {
        get => res ?? (IsCompletedFaulty ? throw ex! : throw new InvalidOperationException("Result was not yet computed"));
        internal set
        {
            res = value;
            continuation?.Invoke();
        }
    }

    public WinterForgeAsyncTask<T> GetAwaiter() => this;

    /// <summary>
    /// Gets the result of the response, and waits if required
    /// </summary>
    /// <returns></returns>
    public T GetResult()
    {
        if (!IsCompleted)
            Wait();
        return Result;
    }

    /// <summary>
    /// Sets the continuation action for this task
    /// </summary>
    /// <param name="continuation"></param>
    public void OnCompleted(Action continuation)
    {
        var scope = threadLock.EnterScope();
        if (IsCompleted)
            continuation();
        else
            this.continuation = continuation;

        scope.Dispose();
    }

    public bool Wait(TimeSpan? timeout = null)
    {
        Task t = Task.Run(() =>
        {
            while (!IsCompleted)
                continue;
        });

        return t.Wait(timeout is null ? Timeout.Infinite : (int)timeout.Value.TotalMilliseconds);
    }
}