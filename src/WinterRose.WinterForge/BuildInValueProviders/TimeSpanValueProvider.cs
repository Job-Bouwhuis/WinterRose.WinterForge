using WinterRose.WinterForgeSerializing.Workers;

namespace WinterRose.WinterForgeSerializing.BuildInValueProviders
{
    class TimeSpanValueProvider : CustomValueProvider<TimeSpan>
    {
        public override TimeSpan CreateObject(object value, WinterForgeVM executor)
        {
            if (value is string s)
            {
                s = s[1..^1];
                return TimeSpan.Parse(s);
            }
            throw new InvalidOperationException("Passed timespan value to convert was not of type string.");
        }

        public override object CreateString(TimeSpan obj, ObjectSerializer serializer)
        {
            return $"\"{obj}\"";
        }
    }

}
