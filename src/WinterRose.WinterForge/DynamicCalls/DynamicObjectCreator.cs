using System;
using System.Collections.Concurrent;
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
        static ConcurrentDictionary<Type, ConstructorInfo[]> constructorCache = [];

        public static object CreateInstanceWithArguments(Type targetType, List<object> argumentStrings)
        {
            argumentStrings = ResolveArgumentTypes(argumentStrings);


            if (WinterForge.SupportedPrimitives.Contains(targetType) && argumentStrings.Count == 1)
                return TypeWorker.CastPrimitive(argumentStrings[0], targetType);

            if (!constructorCache.TryGetValue(targetType, out ConstructorInfo[] constructors))
                constructorCache[targetType] = constructors = targetType.GetConstructors(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.CreateInstance);

            if (argumentStrings.Count == 0)
            {
                foreach (var c in constructors)
                    if (c.GetParameters().Length == 0)
                        return c.Invoke(Array.Empty<object>());

                if (targetType.IsValueType)
                    return Activator.CreateInstance(targetType);
            }

            ConstructorInfo? bestMatch = null;
            object[] bestConvertedArgs = Array.Empty<object>();
            int bestScore = -1;

            foreach (var constructor in constructors)
            {
                ParameterInfo[] parameters = constructor.GetParameters();
                if (argumentStrings.Count > parameters.Length)
                    continue;

                object[] tempConverted = new object[parameters.Length];
                bool match = true;
                int score = 0;

                for (int i = 0; i < parameters.Length; i++)
                {
                    Type paramType = parameters[i].ParameterType;

                    if (i < argumentStrings.Count)
                    {
                        object arg = argumentStrings[i];

                        if (paramType == typeof(object))
                        {
                            tempConverted[i] = arg;
                            continue;
                        }

                        if (arg != null && arg.GetType() == paramType)
                        {
                            tempConverted[i] = arg;
                            score++;
                            continue;
                        }

                        if (!TryConvertArgument(arg, paramType, out object convertedArg))
                        {
                            match = false;
                            break;
                        }

                        tempConverted[i] = convertedArg;
                    }
                    else
                    {
                        // Argument missing, try default value
                        if (parameters[i].HasDefaultValue)
                            tempConverted[i] = parameters[i].DefaultValue;
                        else
                        {
                            match = false;
                            break;
                        }
                    }
                }

                if (match && score > bestScore)
                {
                    bestMatch = constructor;
                    bestConvertedArgs = tempConverted;
                    bestScore = score;
                }
            }

            if (bestMatch != null)
                return bestMatch.Invoke(bestConvertedArgs);

            string s = $"No matching constructor found for type '{targetType.Name}' with arguments: {string.Join(", ", argumentStrings.Select(a => a?.GetType().Name ?? "null"))}";
            throw new WinterForgeSerializeException(targetType, s);
        }

        public static List<object> ResolveArgumentTypes(List<object> argumentStrings)
        {
            var resolvedArguments = new List<object>();

            foreach (var argument in argumentStrings)
            {
                if (argument is string s)
                {
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
            var targetTypes = parameters.Select(p => p.ParameterType).ToArray();
            return TryConvertArguments(inputArgs, targetTypes, out convertedArgs);
        }

        internal static bool TryConvertArguments(List<object> inputArgs, Type[] targetTypes, out object[] convertedArgs)
        {
            convertedArgs = new object[targetTypes.Length];

            for (int i = 0; i < targetTypes.Length; i++)
            {
                object input = inputArgs[i];
                Type targetType = targetTypes[i];

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
                        convertedArgs[i] = Convert.ChangeType(s, targetType);
                        continue;
                    }

                    convertedArgs[i] = Convert.ChangeType(input, targetType);
                }
                catch
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryConvertArgument(object? input, Type targetType, out object converted)
        {
            converted = null!;

            if (targetType == typeof(object))
            {
                converted = input!;
                return true;
            }

            if (input == null)
            {
                if (!targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null)
                {
                    converted = null!;
                    return true;
                }
                return false; // cannot assign null to non-nullable value type
            }

            Type inputType = input.GetType();

            // exact match or assignable
            if (targetType.IsAssignableFrom(inputType))
            {
                converted = input;
                return true;
            }

            try
            {
                converted = Convert.ChangeType(input, targetType);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
