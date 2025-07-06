namespace WinterRose.WinterForgeSerializing;

/// <summary>
/// Represents an asynchronous serialization task
/// </summary>
public class WinterForgeSerializationTask : WinterForgeAsyncTask<string>
{
    /// <summary>
    /// Gets the string, and waits if required
    /// </summary>
    /// <param name="task"></param>
    public static implicit operator string(WinterForgeSerializationTask task)
    {
        if (!task.IsCompleted)
            task.Wait();
        return task.Result;
    }
}