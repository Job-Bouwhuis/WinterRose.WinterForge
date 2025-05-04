using WinterRose.WinterForgeSerializing.Workers;

namespace WinterRose.WinterForgeSerializing.BuildInValueProviders
{
    class TimeSpanValueProvider : CustomValueProvider<TimeSpan>
    {
        public override TimeSpan CreateObject(string value, InstructionExecutor executor)
        {
            return TimeSpan.Parse(value);
        }

        public override string CreateString(TimeSpan obj, ObjectSerializer serializer)
        {
            return obj.ToString();
        }
    }

}
