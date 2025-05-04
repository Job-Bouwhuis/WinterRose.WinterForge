using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinterRose.WinterForgeSerializing.Workers;

namespace WinterRose.WinterForgeSerializing.BuildInValueProviders
{
    internal class TypeValueProvider : CustomValueProvider<Type>
    {
        public override Type? CreateObject(string value, InstructionExecutor executor)
        {
            return TypeWorker.FindType(value);
        }
        public override string CreateString(Type obj, ObjectSerializer serializer) => throw new NotImplementedException();
    }
}
