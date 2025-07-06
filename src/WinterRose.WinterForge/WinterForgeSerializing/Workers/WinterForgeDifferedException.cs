
namespace WinterRose.WinterForgeSerializing.Workers;

[Serializable]
internal class WinterForgeDifferedException : Exception
{
    public WinterForgeDifferedException()
    {
    }

    public WinterForgeDifferedException(string? message) : base(message)
    {
    }

    public WinterForgeDifferedException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
