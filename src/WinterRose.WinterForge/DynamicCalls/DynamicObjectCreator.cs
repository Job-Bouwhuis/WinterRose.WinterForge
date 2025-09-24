using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using WinterRose.Reflection;

namespace WinterRose.WinterForgeSerializing.Workers
{
    public static class DynamicObjectCreator
    {
        static Dictionary<Type, ConstructorInfo[]> constructorCache = [];

        public static object CreateInstanceWithArguments(Type targetType, List<object> argumentStrings)
        {
            if (argumentStrings.Count == 0 && targetType.IsValueType)
                return Activator.CreateInstance(targetType);

            if (WinterForge.SupportedPrimitives.Contains(targetType)
                && argumentStrings.Count == 1)
            {
                return TypeWorker.CastPrimitive(argumentStrings[0], targetType);
            }

            if(!constructorCache.TryGetValue(targetType, out ConstructorInfo[] constructors))
                    constructorCache[targetType] = constructors = targetType.GetConstructors(
                        BindingFlags.Instance | BindingFlags.Public |
                        BindingFlags.NonPublic | BindingFlags.CreateInstance);

            if(argumentStrings.Count == 0)
                foreach(var c in constructors)
                    if (c.GetParameters().Length == 0)
                        return c.Invoke([]);

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
            throw new WinterForgeSerializeException(targetType, s);
        }

        // This method will resolve argument types to their correct types
        public static List<object> ResolveArgumentTypes(List<object> argumentStrings)
        {
            var resolvedArguments = new List<object>();

            foreach (var argument in argumentStrings)
            {
                if (argument is string s)
                {
                    // Try to detect and parse into the most appropriate type
                    object parsed = TryParsePrimitive(s);
                    resolvedArguments.Add(parsed);
                }
                else
                {
                    resolvedArguments.Add(argument);
                }
            }

            return resolvedArguments;
        }

        private static object TryParsePrimitive(string s)
        {
            if (bool.TryParse(s, out var boolResult))
                return boolResult;

            if (int.TryParse(s, out var intResult))
                return intResult;

            if (long.TryParse(s, out var longResult))
                return longResult;

            if (decimal.TryParse(s, out var decimalResult))
            {
                if (float.TryParse(s, out var floatResult) && s.IndexOf('.') < 7) 
                    return floatResult;
                if (double.TryParse(s, out var doubleResult))
                    return doubleResult;
                return decimalResult; 
            }

            return s;
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
