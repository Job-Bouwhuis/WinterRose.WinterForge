
using System.Runtime.CompilerServices;
using System.Threading;
using WinterRose.WinterForgeSerializing.Logging;

namespace WinterRose.WinterForgeSerializing;

/// <summary>
/// Represents an asynchrounous task for <see cref="WinterForge"/> where <typeparamref name="T"/> is the desired return type for the deserialization process
/// </summary>
/// <typeparam name="T"></typeparam>
public class WinterForgeDeserializationTask<T> : WinterForgeAsyncTask<T>, INotifyCompletion
{
    /// <summary>
    /// Gets the result, and waits if required
    /// </summary>
    /// <param name="task"></param>
    public static implicit operator T(WinterForgeDeserializationTask<T> task)
    {
        if (!task.IsCompleted)
            task.Wait();
        return task.Result;
    }
}