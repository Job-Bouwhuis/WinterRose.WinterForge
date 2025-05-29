using System;
using System.Reflection;

namespace WinterRose.AnonymousTypes
{
    /// <summary>
    /// A struct that represents a method in a class that was parsed through the <see cref="AnonymousTypeReader"/>.
    /// </summary>
    public class AnonymousMethod
    {
        /// <summary>
        /// The name of the method.
        /// </summary>
        public string Name { get; }

        ///<summary>
        /// The parameters of the method.
        /// </summary>
        public Type[] Parameters { get; }

        private object Target { get; }
        private MethodInfo methodInfo { get; }

        ///<summary>
        /// Creates a new instance of <see cref="AnonymousMethod"/>.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="parameters"></param>
        public AnonymousMethod(string name, object target, MethodInfo info, Type[] parameters)
        {
            Name = name;
            Parameters = parameters;
            Target = target;
            methodInfo = info;
        }

        /// <summary>
        /// Invokes the method.
        /// </summary>
        /// <param name="args"></param>
        /// <returns>The value the method returned. value is always null if the method returns <see cref="void"/></returns>
        public object? Invoke(params object[] args) => methodInfo.Invoke(Target, args);
    }
}
