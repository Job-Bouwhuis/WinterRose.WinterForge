using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using WinterRose.AnonymousTypes;

namespace WinterRose.Reflection
{
    internal class AnonymousMember : MemberData
    {
        public AnonymousMember(string memberName, Anonymous anonymous)
        {
            this.name = memberName;
            this.anonymous = anonymous;
        }

        private string name;
        private Anonymous anonymous;

        public override string Name => name;

        public override MemberTypes MemberType => MemberTypes.Custom;

        public override Type Type
        {
            get
            {
                try
                {
                    return anonymous[name]?.GetType() ?? throw new AnonymousFieldDoesntExistException(name);
                }
                catch (Exception ex)
                {
                    return typeof(void);
                }
            }
        }

        public override FieldAttributes FieldAttributes => FieldAttributes.Public;

        public override PropertyAttributes PropertyAttributes => PropertyAttributes.None;

        public override bool IsPublic => true;

        public override bool PropertyHasSetter => true;

        public override bool IsStatic => false;

        public override bool IsInitOnly => false;

        public override bool IsLiteral => false;

        public override bool CanWrite => true;

        public override bool ByRef => false;

        public override bool IsValid => !string.IsNullOrWhiteSpace(name) && anonymous is not null;

        public override void SetFieldValue<T>(ref object? obj, T value)
        {
            SetValue(ref obj, value);
        }
        public override void SetPropertyValue<T>(ref object? obj, T value)
        {
            SetValue(ref obj, value);
        }
        public override void SetValue(ref object? obj, object? value)
        {
            if (obj is not Anonymous an)
                throw new Exception("obj was not of type Anonymous, or a derived type of Anonymous");

            an.runtimeVariables[name] = value;
        }
        public override object? GetValue(ref object? obj)
        {
            if (obj is not Anonymous an)
                throw new Exception("obj was not of type Anonymous, or a derived type of Anonymous");

            if(an.runtimeVariables.TryGetValue(name, out object? val))
                return val;

            throw new FieldNotFoundException($"Field {name} not found on Anonymous type");
        }
        protected override string ToDebuggerString() => $"Anonymous member <{Name}>";
    }
}
