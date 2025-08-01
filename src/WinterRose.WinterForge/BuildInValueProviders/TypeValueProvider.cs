﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinterRose.WinterForgeSerializing;
using WinterRose.WinterForgeSerializing.Workers;

namespace WinterRose.WinterForgeSerializing.BuildInValueProviders
{
    internal class TypeValueProvider : CustomValueProvider<Type>
    {
        public override Type? CreateObject(object value, InstructionExecutor executor)
        {
            return InstructionExecutor.ResolveType((string)value);
        }
        public override object CreateString(Type obj, ObjectSerializer serializer)
        {
            return $"_type({serializer.ParseTypeName(obj)})";
        }
    }
}
