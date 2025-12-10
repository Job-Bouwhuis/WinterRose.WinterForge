using WinterRose.WinterForgeSerializing.Workers;

namespace WinterRose.WinterForgeSerializing.BuildInValueProviders
{
    class DateTimeValueProvider : CustomValueProvider<DateTime>
    {
        public override DateTime CreateObject(object value, WinterForgeVM executor)
        {
            return DateTime.Parse((string)value);
        }

        public override object CreateString(DateTime obj, ObjectSerializer serializer)
        {
            return $"\"{obj}\"";
        }
    }

}
