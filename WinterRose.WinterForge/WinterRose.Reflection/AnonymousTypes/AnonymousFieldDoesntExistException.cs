
namespace WinterRose.AnonymousTypes;

[Serializable]
internal class AnonymousFieldDoesntExistException : Exception
{
    public AnonymousFieldDoesntExistException()
    {
    }

    public AnonymousFieldDoesntExistException(string? message) : base(message)
    {
    }

    public AnonymousFieldDoesntExistException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}