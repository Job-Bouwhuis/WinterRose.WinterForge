using WinterRose.WinterForgeSerializing.Workers;

namespace WinterRose.WinterForgeSerializing.BuildInValueProviders
{
    class TimeSpanValueProvider : CustomValueProvider<TimeSpan>
    {
        public override TimeSpan CreateObject(object value, WinterForgeVM executor)
        {
            if(value is string s)
                return TimeSpan.Parse(s);
            if (value is TimeSpan t)
                return t;
            return TimeSpan.Zero;
        }

        public override object CreateString(TimeSpan obj, ObjectSerializer serializer)
        {
            return $"{obj}";
        }
    }

}
