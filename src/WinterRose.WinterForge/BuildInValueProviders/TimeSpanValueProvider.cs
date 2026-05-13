using WinterRose.WinterForgeSerializing.Workers;

namespace WinterRose.WinterForgeSerializing.BuildInValueProviders
{
    class TimeSpanValueProvider : CustomValueProvider<TimeSpan>
    {
        public override TimeSpan CreateObject(object value, WinterForgeVM executor)
        {
            return TimeSpan.Parse((string)value);
        }

        public override object CreateString(TimeSpan obj, ObjectSerializer serializer)
        {
            return $"{obj}";
        }
    }

}
