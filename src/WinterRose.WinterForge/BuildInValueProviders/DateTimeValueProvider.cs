using WinterRose.WinterForgeSerializing.Workers;

namespace WinterRose.WinterForgeSerializing.BuildInValueProviders
{
    class DateTimeValueProvider : CustomValueProvider<DateTime>
    {
        public override DateTime CreateObject(object value, WinterForgeVM executor)
        {
            if(value is string s && s.StartsWith('"') && s.EndsWith('"'))
                return DateTime.Parse(s[1..^1]);
            return DateTime.FromBinary(Convert.ToInt64(value));
        }

        public override object CreateString(DateTime obj, ObjectSerializer serializer)
        {
            return obj.ToBinary();
        }
    }

}
