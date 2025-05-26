using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using WinterRose.Reflection;
using System.Collections;
using System.Reflection.Metadata;
using WinterRose.WinterForgeSerializing.Logging;
using System.Data;

namespace WinterRose.WinterForgeSerializing.Workers
{
    /// <summary>
    /// The system to automatically serialize a runtime object to the <see cref="WinterForge"/> human readable format
    /// </summary>
    /// <param name="progressTracker"></param>
    public class ObjectSerializer(WinterForgeProgressTracker? progressTracker)
    {
        private readonly Dictionary<object, int> cache = [];
        private readonly HashSet<(Type, string)> staticFieldCache = [];
        private int currentKey = 0;

        internal void SerializeAsStatic(Type type, Stream destinationStream)
        {
            ReflectionHelper rh = new(type);
            var members = rh.GetMembers();
            foreach (var member in members)
            {
                if (member.IsStatic)
                {
                    progressTracker?.OnField(type.Name + '.' + member.Name, 0, 0);
                    HandleStaticMember(member, type, rh, destinationStream, progressTracker, true);
                }
            }
        }

        internal void Serialize(object obj, Stream destinationStream, bool isRootCall, bool emitReturn = true)
        {
            if (isRootCall)
            {
                cache.Clear();
                currentKey = 0;
            }

            if (obj == null)
            {
                WriteToStream(destinationStream, "null");
                return;
            }

            if (cache.TryGetValue(obj, out int key))
            {
                WriteToStream(destinationStream, $"_ref({key})");
                return;
            }
            else
                key = currentKey++;

            Type objType = obj.GetType();

            if (WinterForge.SupportedPrimitives.Contains(objType))
            {
                WriteToStream(destinationStream, obj.ToString());
                return;
            }

            string? collection = TryCollection(obj);
            if (collection is not null)
            {
                WriteToStream(destinationStream, collection);

                if (isRootCall)
                    WriteToStream(destinationStream, "\n\nreturn _stack()");

                return;
            }

            if (CustomValueProviderCache.Get(objType, out var provider))
            {
                WriteToStream(destinationStream, provider._CreateString(obj, this));
                return;
            }

            // Write the type and an index for the object

            string name = GetTypeName(objType);


            WriteToStream(destinationStream, $"{name} : {key} {{\n");
            var helper = new ReflectionHelper(ref obj);

            // dont cache the object if it doesnt wish to be cached. or is a struct
            if (objType.GetCustomAttributes<NotCachedAttribute>().FirstOrDefault() is null)
                if (!objType.IsValueType)
                    cache.Add(obj, key);

            progressTracker?.OnInstance($"Serializing {objType.Name}", objType.Name, objType.IsClass, 0, 0);

            FlowHookItem item = FlowHookCache.Get(objType);
            if (item.Any)
                item.InvokeBeforeSerialize(obj);

            SerializePropertiesAndFields(obj, helper, destinationStream, progressTracker);
            WriteToStream(destinationStream, "}\n");

            if (isRootCall)
                WriteToStream(destinationStream, "\n\nreturn " + key);
            destinationStream.Flush();
        }

        private string GetTypeName(Type t)
        {
            if (!t.IsGenericType)
                return t.FullName ?? t.Name;

            string mainTypeName = t.GetGenericTypeDefinition().FullName!;
            mainTypeName = mainTypeName[..mainTypeName.IndexOf('`')]; // remove `N

            Type[] genericArgs = t.GetGenericArguments();
            string[] genericNames = new string[genericArgs.Length];
            for (int i = 0; i < genericArgs.Length; i++)
            {
                genericNames[i] = GetTypeName(genericArgs[i]); // recursively resolve nested generic types
            }

            return $"{mainTypeName}<{string.Join(", ", genericNames)}>";
        }


        private void SerializePropertiesAndFields(object obj, ReflectionHelper rh, Stream destinationStream, WinterForgeProgressTracker? progressTracker)
        {
            Type objType = obj.GetType();

            bool hasIncludeAllProperties =
                objType.GetCustomAttributes<IncludeAllPropertiesAttribute>().FirstOrDefault() is not null;
            bool hasIncludeAllPrivateFields =
                objType.GetCustomAttributes<IncludePrivateFieldsAttribute>().FirstOrDefault() is not null;

            // Serialize properties
            var members = rh.GetMembers();
            foreach (var member in members)
            {
                if (member.IsStatic)
                {
                    progressTracker?.OnField(objType.Name + '.' + member.Name, 0, 0);
                    HandleStaticMember(member, objType, rh, destinationStream, progressTracker, false);
                    continue;
                }

                if (IsMemberSerializable(member, hasIncludeAllPrivateFields, hasIncludeAllProperties))
                {
                    progressTracker?.OnField(member.Name, 0, 0);
                    CommitValue(obj, destinationStream, member);
                }
            }
        }

        private bool IsMemberSerializable(MemberData member, bool hasIncludeAllPrivateFields, bool hasIncludeAllProperties)
        {
            if (!member.CanWrite)
                return false; // ignore unwritable members

            bool hasIncludeAttr = member.Attributes.Any(x => x is IncludeWithSerializationAttribute);

            if (!member.IsPublic)
            {
                if (!hasIncludeAttr && !hasIncludeAllPrivateFields)
                    return false;

                // Skip property backing fields regardless of private field setting unless explicitly included
                if (member.Name.Contains('<') && !hasIncludeAttr)
                    return false;
            }

            if (member.Attributes.Any(x => x is ExcludeFromSerializationAttribute))
                return false;

            if (member.MemberType == MemberTypes.Property)
            {
                if (!hasIncludeAttr && !hasIncludeAllProperties)
                    return false;
            }

            return true;
        }


        private void HandleStaticMember(MemberData member, Type type, ReflectionHelper rh,
            Stream destinationStream, WinterForgeProgressTracker? progressTracker, bool asStaticClass)
        {
            if (!asStaticClass)
                if(member.IsPublic && member.GetAttribute<IncludeWithSerializationAttribute>() is null)
                    return;
            if (IsMemberSerializable(member, false, false))
            {
                CommitStaticValue(type, destinationStream, member);
            }
        }

        private void CommitStaticValue(Type type, Stream destinationStream, MemberData member)
        {
            if (staticFieldCache.Contains((type, member.Name)))
                return;
            staticFieldCache.Add((type, member.Name));

            object value = member.GetValue(null);

            string serializedString = SerializeValue(value);
            int linePos = serializedString.IndexOf('\n');
            if (linePos != -1)
            {
                if (serializedString.StartsWith('"') && serializedString.EndsWith('"'))
                {
                    WriteToStream(destinationStream, $"{type.FullName}->{member.Name} = {serializedString}");
                    WriteToStream(destinationStream, ";\n");
                    return;
                }
                string line = serializedString[0..linePos];
                linePos = line.IndexOf(':');
                if (linePos != -1)
                {
                    ReadOnlySpan<char> indexStart = line.AsSpan()[linePos..];
                    int len = indexStart.Length;

                    // Trim from the end while chars are not numeric
                    while (len > 0 && !char.IsDigit(indexStart[len - 1]))
                        len--;

                    indexStart = indexStart[1..len].Trim();
                    int key = int.Parse(indexStart);

                    WriteToStream(destinationStream, serializedString);
                    WriteToStream(destinationStream, $"{type.FullName}->{member.Name} = _ref({key});\n");
                    return;
                }
            }

            if (isDecimalNumber(serializedString))
                serializedString = serializedString.Replace(',', '.');

            WriteToStream(destinationStream, $"{type.FullName}->{member.Name} = {serializedString}");
            if (!serializedString.Contains('['))
                WriteToStream(destinationStream, ";\n");
        }

        private void CommitValue(object obj, Stream destinationStream, MemberData member)
        {
            object value = member.GetValue(obj);

            string serializedString = SerializeValue(value);
            int linePos = serializedString.IndexOf('\n');
            if (linePos != -1)
            {
                if (serializedString.StartsWith('"') && serializedString.EndsWith('"'))
                {
                    WriteToStream(destinationStream, $"{member.Name} = {serializedString}");
                    WriteToStream(destinationStream, ";\n");
                    return;
                }
                string line = serializedString[0..linePos];
                linePos = line.IndexOf(':');
                if (linePos != -1)
                {
                    ReadOnlySpan<char> indexStart = line.AsSpan()[linePos..];
                    int len = indexStart.Length;

                    // Trim from the end while chars are not numeric
                    while (len > 0 && !char.IsDigit(indexStart[len - 1]))
                        len--;

                    indexStart = indexStart[1..len].Trim();
                    int key = int.Parse(indexStart);

                    WriteToStream(destinationStream, serializedString);
                    WriteToStream(destinationStream, $"{member.Name} = _ref({key});\n");
                    return;
                }
            }

            if (isDecimalNumber(serializedString))
                serializedString = serializedString.Replace(',', '.');

            WriteToStream(destinationStream, $"{member.Name} = {serializedString}");
            if (!serializedString.Contains('['))
                WriteToStream(destinationStream, ";\n");
        }
        private bool isDecimalNumber(string serializedString)
        {
            bool lastDigit = false;
            bool expectNextIsPlus = false;
            foreach (char c in serializedString)
            {
                if (expectNextIsPlus)
                    if (c == '+')
                    {
                        expectNextIsPlus = false;
                        continue;
                    }
                    else
                        return false;
                if (char.IsDigit(c))
                {
                    lastDigit = true;
                    continue;
                }
                else if (c == ',' || c == '.')
                {
                    if (!lastDigit)
                        return false;
                }
                else if (c == 'E')
                    expectNextIsPlus = true;
                else
                    return false;
                lastDigit = false;
            }
            return true;
        }
        private string SerializeValue(object value)
        {
            if (value == null)
                return "null";

            Type valueType = value.GetType();

            if (value is string s)
            {
                if (s.Contains('\n'))
                    return $"\"\"\"\"\"{s}\"\"\"\"\"";
                return $"\"{value}\"";
            }

            // Check if the value is a primitive or string
            if (valueType.IsPrimitive)
                return value.ToString();

            // Handle arrays, lists, and collections (nested objects)
            string? collection = TryCollection(value);
            if (collection is not null)
                return collection;

            // If the value is a nested object, recursively serialize it
            return RecursiveSerialization(value); // We can reuse the same serializer method for nested objects
        }
        private string? TryCollection(object value)
        {
            if (value is not IEnumerable collection)
                return null;

            Type valueType = value.GetType();

            bool isArray = valueType.IsArray;

            Type? elementType = isArray
                ? valueType.GetElementType()
                : valueType.IsGenericType
                    ? valueType.GetGenericArguments()[0]
                    : null;

            // Respect custom attribute blocking serialization
            if (!isArray)
            {
                SerializeAsAttributeINTERNAL? attr =
                    valueType.GetCustomAttribute<SerializeAsAttributeINTERNAL>();
                if (attr is not null && attr.Type != typeof(IEnumerable))
                    return null;
            }

            StringBuilder sb = new StringBuilder();
            if (elementType is null)
                sb.Append($"<System.Object>[\n");
            else
                sb.Append($"<{ParseTypeName(elementType)}>[\n");

            bool first = true;
            foreach (var item in collection)
            {
                if (!first)
                    sb.Append("\n,\n");

                sb.Append(SerializeValue(item));
                first = false;
            }

            sb.Append("\n]\n");

            return sb.ToString();
        }

        private string ParseTypeName(Type elementType)
        {
            if (!elementType.IsGenericType)
                return elementType.FullName;

            Type[] genericTypes = elementType.GenericTypeArguments;
            string[] genericTypeNames = [.. genericTypes.Select(ParseTypeName)];

            int indexOfTypeNameEnd = elementType.FullName.IndexOf('`');
            return elementType.FullName[0..indexOfTypeNameEnd] + "<" + string.Join(",", genericTypeNames) + ">";
        }

        private void WriteToStream(Stream stream, string content)
        {
            byte[] contentBytes = Encoding.UTF8.GetBytes(content);
            stream.Write(contentBytes, 0, contentBytes.Length);
        }
        private string RecursiveSerialization(object obj)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                Serialize(obj, ms, false);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        /// <summary>
        /// Serializes the given object to a string using the <see cref="WinterForge"/> serialization system
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public string SerializeToString(object obj)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                Serialize(obj, ms, true);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }
        /// <summary>
        /// Serializes the given object directly to a file
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="filePath"></param>
        public void SerializeToFile(object obj, string filePath)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                Serialize(obj, fs, true);
            }
        }
        /// <summary>
        /// Serializes the given object to the given stream
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="destination"></param>
        public void Serialize(object obj, Stream destination) => Serialize(obj, destination, true);
    }
}
