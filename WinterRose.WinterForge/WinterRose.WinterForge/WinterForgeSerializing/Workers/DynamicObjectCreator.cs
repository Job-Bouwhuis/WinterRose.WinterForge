using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using WinterRose.Reflection;

namespace WinterRose.WinterForgeSerializing.Workers
{
    public class DynamicObjectCreator
    {
        public static object CreateInstanceWithArguments(Type targetType, List<object> argumentStrings)
        {
            if (argumentStrings.Count == 0 && targetType.IsValueType)
                return Activator.CreateInstance(targetType);

            if (WinterForge.SupportedPrimitives.Contains(targetType)
                && argumentStrings.Count == 1)
            {
                return TypeWorker.CastPrimitive(argumentStrings[0], targetType);
            }

            ConstructorInfo[] constructors = targetType.GetConstructors(
             BindingFlags.Instance | BindingFlags.Public |
             BindingFlags.NonPublic | BindingFlags.CreateInstance);

            foreach (var constructor in constructors)
            {
                ParameterInfo[] parameters = constructor.GetParameters();

                if (parameters.Length == argumentStrings.Count)
                    if (TryConvertArguments(argumentStrings, parameters, out object[] convertedArgs))
                        return constructor.Invoke(convertedArgs);
            }

            string s = $"No matching constructor found for type '{targetType.Name}' that takes these arguments: ";
            bool first = true;
            foreach (var arg in argumentStrings)
            {
                if (!first)
                    s += ", ";
                if (arg is not string)
                    s += arg.GetType().Name;
                else
                    s += arg;

                first = false;
            }
            throw new Exception(s);
        }

        // This method will resolve argument types to their correct types
        public static List<object> ResolveArgumentTypes(List<object> argumentStrings)
        {
            var resolvedArguments = new List<object>();

            foreach (var argument in argumentStrings)
            {
                if (argument is string s)
                {
                    if (int.TryParse(s, out var intResult))
                        resolvedArguments.Add(intResult); // Integer
                    else if (float.TryParse(s, out var floatResult))
                    {
                        resolvedArguments.Add(floatResult); // Float
                    }
                    else if (double.TryParse(s, out var doubleResult))
                    {
                        resolvedArguments.Add(doubleResult); // Double
                    }
                    else if (s == "true" || s == "false")
                    {
                        resolvedArguments.Add(bool.Parse(s)); // Boolean
                    }
                    else
                        resolvedArguments.Add(s);
                }
                else
                    resolvedArguments.Add(argument);

            }

            return resolvedArguments;
        }

        private static bool TryConvertArguments(List<object> inputArgs, ParameterInfo[] parameters, out object[] convertedArgs)
        {
            convertedArgs = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                object input = inputArgs[i];
                Type targetType = parameters[i].ParameterType;

                if (input == null)
                {
                    if (!targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null)
                    {
                        convertedArgs[i] = null;
                        continue;
                    }
                    return false;
                }

                if (input.GetType() == targetType || targetType.IsAssignableFrom(input.GetType()))
                {
                    convertedArgs[i] = input;
                    continue;
                }

                try
                {
                    if (input is string s)
                    {
                        var converted = Convert.ChangeType(s, targetType);
                        convertedArgs[i] = converted!;
                        continue;
                    }
                    else
                    {
                        convertedArgs[i] = Convert.ChangeType(input, targetType);
                    }
                }
                catch
                {
                    return false;
                }
            }

            return true;
        }

    }
}
