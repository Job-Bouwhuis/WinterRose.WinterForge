using WinterRose.WinterForgeSerializing.Workers;

namespace WinterRose.WinterForgeSerializing.BuildInValueProviders
{
    class TimeOnlyValueProvider : CustomValueProvider<TimeOnly>
    {
        public override TimeOnly CreateObject(string value, InstructionExecutor executor)
        {
            return TimeOnly.Parse(value);
        }

        public override string CreateString(TimeOnly obj, ObjectSerializer serializer)
        {
            return obj.ToString();
        }
    }

}
