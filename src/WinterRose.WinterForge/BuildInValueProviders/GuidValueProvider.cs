using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinterRose.WinterForgeSerializing;
using WinterRose.WinterForgeSerializing.Workers;

namespace WinterRose.WinterForgeSerializing
{
    internal class GuidValueProvider : CustomValueProvider<Guid>
    {
        public override Guid CreateObject(object value, WinterForgeVM executor) => Guid.Parse((string)value);
        public override object CreateString(Guid obj, ObjectSerializer serializer) => obj.ToString();
    }
}
