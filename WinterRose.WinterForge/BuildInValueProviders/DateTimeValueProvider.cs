using WinterRose.WinterForgeSerializing.Workers;

namespace WinterRose.WinterForgeSerializing.BuildInValueProviders
{
    class DateTimeValueProvider : CustomValueProvider<DateTime>
    {
        public override DateTime CreateObject(string value, InstructionExecutor executor)
        {
            return DateTime.Parse(value);
        }

        public override string CreateString(DateTime obj, ObjectSerializer serializer)
        {
            return obj.ToString();
        }
    }

}
