
namespace WinterRose.WinterForgeSerializing.Workers;

[Serializable]
internal class WinterForgeSerializeException : Exception
{
    public WinterForgeSerializeException(object obj, string v) : base($"{obj.GetType().FullName} :: {v}")
    {
    }
}