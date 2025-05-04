using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using WinterRose.WinterForgeSerializing;

namespace WinterRose.WinterForgeSerialization.Workers
{
    public class DynamicMethodInvoker
    {
        public static object? InvokeMethodWithArguments(Type targetType, string methodName, object? target, object[] arguments)
        {
            MethodInfo[] methods = targetType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

            MethodInfo matchedMethod = GetBestMatchingMethod(methods, methodName, arguments, out arguments);

            if (matchedMethod == null)
                throw new Exception($"No matching method found for method '{methodName}' with the given arguments.");

            object? result = matchedMethod.Invoke(target, arguments);
            return result;
        }

        private static MethodInfo? GetBestMatchingMethod(MethodInfo[] methods, string methodName, object[] arguments, out object[] convertedArguments)
        {
            MethodInfo? bestMatch = null;

            convertedArguments = [];

            foreach (var method in methods)
            {
                if (method.Name == methodName)
                {
                    ParameterInfo[] parameters = method.GetParameters();
                    convertedArguments = new object[parameters.Length];
                    if (parameters.Length == arguments.Length)
                    {
                        bool match = true;
                        for (int i = 0; i < parameters.Length; i++)
                        {
                            Type paramType = parameters[i].ParameterType;
                            object arg = arguments[i];

                            var convertSuccess = TryConvertArgument(arg, paramType, out object convertedArg);
                            if (arg != null && !convertSuccess)
                            {
                                match = false;
                                break;
                            }
                            convertedArguments[i] = convertedArg;
                        }

                        if (match)
                        {
                            if (bestMatch == null)
                            {
                                bestMatch = method;
                                break;
                            }
                        }
                    }
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
