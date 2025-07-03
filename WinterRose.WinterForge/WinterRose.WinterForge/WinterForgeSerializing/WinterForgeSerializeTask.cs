namespace WinterRose.WinterForgeSerializing;

public class WinterForgeSerializeTask : WinterForgeAsyncTask<string>
{
    /// <summary>
    /// Gets the string, and waits if required
    /// </summary>
    /// <param name="task"></param>
    public static implicit operator string(WinterForgeSerializeTask task)
    {
        if (!task.IsCompleted)
            task.Wait();
        return task.Result;
    }
}