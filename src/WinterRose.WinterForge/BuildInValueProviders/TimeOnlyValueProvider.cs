using WinterRose.WinterForgeSerializing.Workers;

namespace WinterRose.WinterForgeSerializing.BuildInValueProviders
{
    class TimeOnlyValueProvider : CustomValueProvider<TimeOnly>
    {
        public override TimeOnly CreateObject(object value, WinterForgeVM executor)
        {
            return TimeOnly.Parse((string)value);
        }

        public override object CreateString(TimeOnly obj, ObjectSerializer serializer)
        {
            return $"\"{obj}\"";
        }
    }

}
