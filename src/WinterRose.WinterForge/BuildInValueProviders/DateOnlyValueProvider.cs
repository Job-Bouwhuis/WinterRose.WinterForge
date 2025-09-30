using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinterRose.Reflection;
using WinterRose.WinterForgeSerializing;
using WinterRose.WinterForgeSerializing.Workers;

namespace WinterRose.WinterForgeSerializing.BuildInValueProviders
{
    class DateOnlyValueProvider : CustomValueProvider<DateOnly>
    {
        public override DateOnly CreateObject(object value, WinterForgeVM executor)
        {
            return DateOnly.Parse((string)value);
        }

        public override object CreateString(DateOnly obj, ObjectSerializer serializer)
        {
            return obj.ToString();
        }
    }
}
