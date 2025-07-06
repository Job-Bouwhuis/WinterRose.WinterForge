
namespace WinterRose.Reflection;

[Serializable]
public class FieldNotFoundException : Exception
{
    public FieldNotFoundException()
    {
    }

    public FieldNotFoundException(string? message) : base(message)
    {
    }

    public FieldNotFoundException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}