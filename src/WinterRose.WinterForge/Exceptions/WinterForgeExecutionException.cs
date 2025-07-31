
namespace WinterRose.WinterForgeSerializing.Workers;

[Serializable]
internal class WinterForgeExecutionException : Exception
{
    public WinterForgeExecutionException()
    {
    }

    public WinterForgeExecutionException(string? message) : base(message)
    {
    }

    public WinterForgeExecutionException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}