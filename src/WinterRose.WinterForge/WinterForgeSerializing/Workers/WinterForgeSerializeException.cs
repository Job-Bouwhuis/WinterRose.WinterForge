
namespace WinterRose.WinterForgeSerializing.Workers;

[Serializable]
internal class WinterForgeSerializeException : Exception
{
    public WinterForgeSerializeException(object obj, string extraMessage) : base($"{obj.GetType().FullName} :: {extraMessage}")
    {
    }
    public WinterForgeSerializeException(Type type, string extraMessage) : base($"{type.FullName} :: {extraMessage}")
    {
    }
}