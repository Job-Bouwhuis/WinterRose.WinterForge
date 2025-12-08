using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using WinterRose.WinterForgeSerializing;
using WinterRose.WinterForgeSerializing.Formatting;
using System.Linq;

namespace WinterRose.WinterForgeSerializing.Workers
{
    public class DynamicMethodInvoker
    {
        public static object? InvokeMethodWithArguments(Type targetType, string methodName, object? target, object[] args)
        {
            MethodInfo[] methods;

            if (targetType.FullName is "Internal.Console")
                targetType = typeof(Console);

            methods = targetType.GetMethods(BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Static |
                BindingFlags.Instance |
                BindingFlags.FlattenHierarchy);

            MethodInfo matchedMethod = GetBestMatchingMethod(methods, methodName, args, out object[] arguments);

            if (matchedMethod == null)
            {
                string argsString = args.Length == 0 ? "[]" : "[" + string.Join(", ", args.Select(a => ObjectSerializer.ParseTypeName(a.GetType()))) + "]";
                throw new Exception($"No matching method found for method '{methodName}' with the given argument types: {argsString} ");
            }

            object? result = matchedMethod.Invoke(target, arguments);
            if (matchedMethod.ReturnType == typeof(void))
                return typeof(void);
            return result;
        }

        private static MethodInfo? GetBestMatchingMethod(MethodInfo[] methods, string methodName, object[] arguments, out object[] convertedArguments)
        {
            MethodInfo? bestMatch = null;
            convertedArguments = Array.Empty<object>();
            int bestScore = -1;

            foreach (var method in methods)
            {
                if (method.Name != methodName)
                    continue;

                ParameterInfo[] parameters = method.GetParameters();

                // Skip if too many arguments
                if (arguments.Length > parameters.Length)
                    continue;

                object[] tempConverted = new object[parameters.Length];
                bool match = true;
                int score = 0;

                for (int i = 0; i < parameters.Length; i++)
                {
                    Type paramType = parameters[i].ParameterType;

                    if (i < arguments.Length)
                    {
                        object arg = arguments[i];

                        // Exact match preferred
                        if (paramType == typeof(object))
                        {
                            tempConverted[i] = arg;
                            continue;
                        }

                        if (arg != null && arg.GetType() == paramType)
                        {
                            tempConverted[i] = arg;
                            score++; // exact match
                            continue;
                        }

                        var convertSuccess = TryConvertArgument(arg, paramType, out object convertedArg);
                        if (arg != null && !convertSuccess)
                        {
                            match = false;
                            break;
                        }

                        tempConverted[i] = convertedArg;
                    }
                    else
                    {
                        // Argument was not provided, check for default value
                        if (parameters[i].HasDefaultValue)
                        {
                            tempConverted[i] = parameters[i].DefaultValue;
                        }
                        else
                        {
                            match = false;
                            break;
                        }
                    }
                }

                if (match && score > bestScore)
                {
                    bestMatch = method;
                    convertedArguments = tempConverted;
                    bestScore = score;
                }
            }

            return bestMatch;
        }

        public static List<object> ResolveArgumentTypes(object[] argumentStrings)
        {
            var resolvedArguments = new List<object>();

            foreach (var argument in argumentStrings)
            {
                if (argument is string s)
                {
                    if (int.TryParse(s, out var intResult))
                        resolvedArguments.Add(intResult);
                    else if (long.TryParse(s, out var longResult))
                        resolvedArguments.Add(longResult);
                    else if (float.TryParse(s, out var floatResult))
                        resolvedArguments.Add(floatResult);
                    else if (double.TryParse(s, out var doubleResult))
                        resolvedArguments.Add(doubleResult);
                    else if (decimal.TryParse(s, out var decimalResult))
                        resolvedArguments.Add(decimalResult);
                    else if (byte.TryParse(s, out var byteResult))
                        resolvedArguments.Add(byteResult);
                    else if (short.TryParse(s, out var shortResult))
                        resolvedArguments.Add(shortResult);
                    else if (ushort.TryParse(s, out var ushortResult))
                        resolvedArguments.Add(ushortResult);
                    else if (uint.TryParse(s, out var uintResult))
                        resolvedArguments.Add(uintResult);
                    else if (ulong.TryParse(s, out var ulongResult))
                        resolvedArguments.Add(ulongResult);
                    else if (s.Equals("true", StringComparison.OrdinalIgnoreCase))
                        resolvedArguments.Add(true);
                    else if (s.Equals("false", StringComparison.OrdinalIgnoreCase))
                        resolvedArguments.Add(false);
                    else
                        resolvedArguments.Add(s); // default string
                }
                else
                    resolvedArguments.Add(argument);
            }

            return resolvedArguments;
        }

        private static bool TryConvertArgument(object input, Type targetType, out object? converted)
        {
            try
            {
                if (input.GetType() == targetType)
                {
                    converted = input;
                    return true;
                }
                else if (input.GetType().IsAssignableTo(targetType))
                {
                    converted = input;
                    return true;
                }
                else if (input is string s)
                {
                    converted = Convert.ChangeType(s, targetType);
                    return converted != null;
                }
                else
                {
                    converted = Convert.ChangeType(input, targetType);
                    return converted != null;
                }
            }
            catch
            {
                converted = null;
                return false;
            }
        }
    }


}
