﻿using System.Collections;
using System.Collections.Concurrent;
using System.Data;
using System.Runtime.InteropServices;
using System.Text;
using WinterRose.AnonymousTypes;
using WinterRose.Reflection;
using WinterRose.WinterForgeSerializing.Instructions;
using WinterRose.WinterForgeSerializing.Logging;

namespace WinterRose.WinterForgeSerializing.Workers
{
    /// <summary>
    /// Used to deserialize a collection of <see cref="Instruction"/>
    /// </summary>
    public class InstructionExecutor : IDisposable
    {
        private abstract class CollectionDefinition
        {
            public abstract ICollection Collection { get; }
        }

        private class ListDefinition : CollectionDefinition
        {
            public ListDefinition(Type itemType, IList newList)
            {
                ElementType = itemType;
                Values = newList;
            }

            public Type ElementType { get; set; }
            public IList Values { get; set; }
            public override ICollection Collection => Values;
        }

        private class DictionaryDefinition : CollectionDefinition
        {
            public DictionaryDefinition(Type keyType, Type valueType, IDictionary values)
            {
                KeyType = keyType;
                ValueType = valueType;
                Values = values;
            }

            public Type KeyType { get; set; }
            public Type ValueType { get; set; }
            public IDictionary Values { get; set; }
            public override ICollection Collection => Values;
        }

        public bool IsDisposed { get; private set; }

        internal WinterForgeProgressTracker? progressTracker;
        private static readonly ConcurrentDictionary<string, Type> typeCache = new();
        private DeserializationContext context = null!;
        private readonly Stack<int> instanceIDStack;
        private readonly Stack<CollectionDefinition> listStack = new();
        private readonly List<DispatchedReference> dispatchedReferences = [];
        private StringBuilder? sb;
        private Instruction current;

        private int instructionIndex = 0;
        private int instructionTotal;

        /// <summary>
        /// Create a new instance of the <see cref="InstructionExecutor"/> to deserialize objects
        /// </summary>
        public InstructionExecutor()
        {
            instanceIDStack = new Stack<int>();
        }

        /// <summary>
        /// Releases the workings 
        /// </summary>
        public void Dispose()
        {
            if (IsDisposed)
                return;
            IsDisposed = true;

            context.Dispose();
            dispatchedReferences.Clear();
            listStack.Clear();
            instanceIDStack.Clear();
        }

        /// <summary>
        /// Deserializes the given instructions and gives either a <see cref="List{object}"/> of objects back, 
        /// or the object on which the return instruction was given
        /// </summary>
        /// <param name="instructions"></param>
        /// <returns></returns>
        public unsafe object? Execute(List<Instruction> instructionCollection)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            var instructions = instructionCollection.ToList();
            instructionTotal = instructions.Count;
            try
            {
                progressTracker?.Report(0);
                progressTracker?.Report("Starting...");

                context = new();

                for (instructionIndex = 0; instructionIndex < instructions.Count; instructionIndex++)
                {
                    Instruction instruction = current = instructions[instructionIndex];
                    switch (instruction.OpCode)
                    {
                        case OpCode.DEFINE:
                            HandleDefine(instruction.Args);
                            break;
                        case OpCode.SET:
                            HandleSet(instruction.Args);
                            break;
                        case OpCode.ANONYMOUS_SET:
                            {
                                if (context.GetObject(instanceIDStack.Peek()) is not AnonymousTypeReader obj)
                                    throw new InvalidOperationException("Anonymous type reader not found on stack for anonymous set operation");

                                progressTracker?.OnField((string)instruction.Args[1], instructionIndex + 1, instructionTotal);

                                Type fieldType = ResolveType((string)instruction.Args[0]);

                                object val = GetArgumentValue(instruction.Args[2], 2, fieldType, val => { });
                                obj.SetMember((string)instruction.Args[1], ref val);
                                break;
                            }
                        case OpCode.PUSH:
                            {
                                object arg = instruction.Args[0];
                                if (arg is string)
                                    arg = GetArgumentValue(arg, 0, typeof(string), delegate { });
                                if (arg is Dispatched)
                                    throw new Exception("Other opcodes rely on PUSH having pushed its value, cant differ reference till later");

                                if (!arg.GetType().IsClass && !arg.GetType().IsPrimitive)
                                {
                                    object* ptr = &arg;
                                    var v = new StructReference(ptr);
                                    context.ValueStack.Push(v);
                                }
                                else
                                    context.ValueStack.Push(arg);
                            }
                            break;
                        case OpCode.START_STR:
                            sb = new StringBuilder();
                            break;
                        case OpCode.STR:
                            if (sb is null)
                                throw new InvalidOperationException("Missing 'START_STR' instruction before 'STR' operation");
                            sb.AppendLine((string)instruction.Args[0]);
                            break;
                        case OpCode.END_STR:
                            if (sb is null)
                                throw new InvalidOperationException("Missing 'START_STR' instruction before 'END_STR' operation");
                            context.ValueStack.Push(sb.ToString());
                            sb = null;
                            break;
                        case OpCode.CALL:
                            HandleCall((string)instruction.Args[0], (int)instruction.Args[1]);
                            break;
                        case OpCode.ELEMENT:
                            HandleAddElement(instruction.Args);
                            break;
                        case OpCode.LIST_START:
                            HandleCreateList(instruction.Args);
                            break;
                        case OpCode.LIST_END:
                            HandleEndList();
                            break;
                        case OpCode.END:
                            HandleEnd();
                            break;
                        case OpCode.RET:
                            {
                                Validate();
                                if (instruction.Args[0] is "_stack()")
                                    return context.ValueStack.Peek();
                                if (instruction.Args[0] == "null")
                                    return null;
                                object val = context.GetObject((int)instruction.Args[0]) ?? throw new Exception($"object with ID {instruction.Args[0]} not found");
                                return val;
                            }
                        case OpCode.PROGRESS:
                            progressTracker?.Report((instructionIndex + 1) / (float)instructions.Count);
                            break;
                        case OpCode.ACCESS:
                            {
                                object o = context.ValueStack.Pop();
                                AccessFilterCache.Validate(o is Type ? (Type)o : o.GetType(), AccessFilterKind.Blacklist, (string)instruction.Args[0]);
                                ReflectionHelper rh = CreateReflectionHelper(ref o, out _);
                                context.ValueStack.Push(rh.GetValueFrom((string)instruction.Args[0]));
                                break;
                            }
                        case OpCode.SETACCESS:
                            {
                                var field = (string)instruction.Args[0];
                                var rawValue = instruction.Args[1];

                                var target = context.ValueStack.Pop();

                                AccessFilterCache.Validate(target is Type ? (Type)target : target.GetType(), AccessFilterKind.Blacklist, field);

                                var helper = CreateReflectionHelper(ref target, out object actual);

                                MemberData member = helper.GetMember(field);

                                object? value = GetArgumentValue(rawValue, 1, member.Type, val =>
                                {
                                    if (member.Type.IsArray)
                                    {
                                        if (member.Type.IsArray)
                                            val = ((IList)val).GetInternalArray();
                                    }

                                    member.SetValue(ref actual, val);
                                });
                                if (value is Dispatched)
                                    continue; // value has been dispatched to be set later
                                if (value is StructReference sr)
                                    value = sr.Get();
                                if (member.Type.IsArray)
                                    value = ((IList)value).GetInternalArray();

                                member.SetValue(ref actual, value);
                            }
                            break;
                        case OpCode.AS:
                            context.MoveStackTo((int)instruction.Args[0]);
                            break;
                        case OpCode.IMPORT:
                            {
                                object? val = WinterForge.DeserializeFromFile((string)instruction.Args[0]);
                                int id = TypeConverter.Convert<int>(instruction.Args[1]);
                                context.AddObject(id, ref val);
                            }
                            break;
                        case OpCode.CREATE_REF:
                            {
                                context.AddObject((int)instruction.Args[0], ref instruction.Args[1]);
                            }
                            break;

                        case OpCode.ADD:
                        case OpCode.SUB:
                        case OpCode.MUL:
                        case OpCode.DIV:
                        case OpCode.MOD:
                        case OpCode.POW:
                            {
                                (decimal a, decimal b) = PopTwoDecimals();

                                decimal result = instruction.OpCode switch
                                {
                                    OpCode.ADD => a + b,
                                    OpCode.SUB => a - b,
                                    OpCode.MUL => a * b,
                                    OpCode.DIV => b == 0 ? throw new WinterForgeExecutionException("Division by zero") : a / b,
                                    OpCode.MOD => b == 0 ? throw new WinterForgeExecutionException("Modulo by zero") : a % b,
                                    OpCode.POW => (decimal)Math.Pow((double)a, (double)b),
                                    _ => throw new InvalidOperationException($"Unsupported arithmetic opcode: {instruction.OpCode}")
                                };

                                context.ValueStack.Push(result);
                                break;
                            }

                        case OpCode.NEG:
                            {
                                object value = context.ValueStack.Pop();
                                value = GetArgumentValue(value, 0, typeof(decimal), null);
                                if (value is Dispatched)
                                    throw new WinterForgeExecutionException("Cant differ usage of in-expression value");

                                context.ValueStack.Push(-(decimal)value);
                                break;
                            }

                        case OpCode.EQ:
                        case OpCode.NEQ:
                        case OpCode.GT:
                        case OpCode.LT:
                        case OpCode.GTE:
                        case OpCode.LTE:
                            {
                                object b = context.ValueStack.Pop();
                                object a = context.ValueStack.Pop();

                                bool result = instruction.OpCode switch
                                {
                                    OpCode.EQ => Equals(a, b),
                                    OpCode.NEQ => !Equals(a, b),
                                    OpCode.GT => Compare(a, b) > 0,
                                    OpCode.LT => Compare(a, b) < 0,
                                    OpCode.GTE => Compare(a, b) >= 0,
                                    OpCode.LTE => Compare(a, b) <= 0,
                                    _ => throw new InvalidOperationException($"Unsupported comparison opcode: {instruction.OpCode}")
                                };

                                context.ValueStack.Push(result);
                                break;
                            }

                        case OpCode.AND:
                        case OpCode.OR:
                        case OpCode.XOR:
                            {
                                bool b = PopBool();
                                bool a = PopBool();

                                bool result = instruction.OpCode switch
                                {
                                    OpCode.AND => a && b,
                                    OpCode.OR => a || b,
                                    OpCode.XOR => a ^ b,
                                    _ => throw new InvalidOperationException($"Unsupported boolean opcode: {instruction.OpCode}")
                                };

                                context.ValueStack.Push(result);
                                break;
                            }

                        case OpCode.NOT:
                            {
                                bool value = PopBool();
                                context.ValueStack.Push(!value);
                                break;
                            }

                        default:
                            throw new WinterForgeExecutionException($"Opcode: {instruction.OpCode} not supported");
                    }
                }

                return new Nothing(context.ObjectTable);
            }
            finally
            {
                progressTracker?.Report(1);
                progressTracker?.Report("Finishing up");

                context.Dispose();
                dispatchedReferences.Clear();
                listStack.Clear();
                instanceIDStack.Clear();
            }
        }

        (decimal, decimal) PopTwoDecimals()
        {
            object b = context.ValueStack.Pop();
            object a = context.ValueStack.Pop();

            a = GetArgumentValue(a, 0, typeof(decimal), null);
            if (a is Dispatched) throw new WinterForgeExecutionException("Cant differ usage of in-expression value");

            b = GetArgumentValue(b, 0, typeof(decimal), null);
            if (b is Dispatched) throw new WinterForgeExecutionException("Cant differ usage of in-expression value");

            return ((decimal)a, (decimal)b);
        }

        bool PopBool()
        {
            object value = context.ValueStack.Pop();
            value = GetArgumentValue(value, 0, typeof(bool), null);
            if (value is Dispatched) throw new WinterForgeExecutionException("Cant differ usage of in-expression value");

            return (bool)value;
        }

        int Compare(object a, object b)
        {
            a = GetArgumentValue(a, 0, typeof(IComparable), null);
            b = GetArgumentValue(b, 0, typeof(IComparable), null);
            if (a is Dispatched || b is Dispatched)
                throw new WinterForgeExecutionException("Cant differ usage of in-expression value");

            if (a is IComparable compA && b != null)
                return compA.CompareTo(b);

            throw new WinterForgeExecutionException("Operands are not comparable");
        }

        private static ReflectionHelper CreateReflectionHelper(ref object o, out object? actualTarget)
        {
            ReflectionHelper rh;
            if (o is StructReference sr)
            {
                rh = new(ref sr.Get());
                actualTarget = sr.Get();
                return rh;
            }
            else if (o is Type t)
            {
                rh = new(t);
                actualTarget = null;
                return rh;
            }
            else
                rh = new(ref o);

            actualTarget = o;
            return rh;
        }

        private void Validate()
        {
            if (dispatchedReferences.Count > 0)
            {
                StringBuilder sb = new("\nOn lines:\n");
                foreach (DispatchedReference d in dispatchedReferences)
                {
                    try
                    {
                        object? obj = context.GetObject(d.RefID);
                        if (obj is null)
                        {
                            sb.AppendLine(d.lineNum.ToString());
                            continue;
                        }

                        d.method(obj);
                    }
                    catch
                    {
                        sb.AppendLine(d.lineNum.ToString());
                    }
                }

                throw new Exception("There were objects referenced in the deserialization that were never defined." + sb.ToString());
            }
        }

        private void HandleEndList()
        {
            ICollection list = listStack.Pop().Collection;
            context.ValueStack.Push(list);
        }
        private void HandleCreateList(object[] args)
        {
            if (args.Length == 0)
                throw new Exception("Expected type to initialize list");
            Type itemType = ResolveType((string)args[0]);

            if (args.Length == 2)
            {
                // its a dictionary
                Type valueType = ResolveType((string)args[1]);
                var newDict = WinterForge.CreateDictionary(itemType, valueType);
                listStack.Push(new DictionaryDefinition(itemType, valueType, newDict));
                return;
            }

            var newList = WinterForge.CreateList(itemType);
            listStack.Push(new ListDefinition(itemType, newList));
        }

        private unsafe void HandleDefine(object[] args)
        {
            var typeName = (string)args[0];
            var id = (int)args[1];

            if (typeName is "Anonymous" || typeName.StartsWith("Anonymous-as-"))
            {
                AnonymousTypeReader obj = new AnonymousTypeReader();
                object o = obj;
                if (typeName.StartsWith("Anonymous-as-"))
                {
                    obj.TypeName = typeName[13..];

                    string[] parts = obj.TypeName.Split('-', StringSplitOptions.RemoveEmptyEntries);
                    // if base type was included, its on index 3
                    if (parts.Length == 3)
                    {
                        obj.BaseType = ResolveType(parts[2]);
                        obj.TypeName = parts[0];
                    }
                }

                context.AddObject(id, ref o);
                instanceIDStack.Push(id);
                string tn = obj.TypeName ?? "Anonymous";
                progressTracker?.OnInstance("Creating " + tn, tn, true, instructionIndex + 1, instructionTotal);
                return;
            }


            int numArgs = (int)args[2];
            Type type = ResolveType(typeName);

            List<object> constrArgs = [];
            for (int i = numArgs - 1; i >= 0; i--)
                constrArgs.Add(context.ValueStack.Pop());

            object instance = DynamicObjectCreator.CreateInstanceWithArguments(type, constrArgs)!;
            context.AddObject(id, ref instance);

            instanceIDStack.Push(id);

            FlowHookItem item = FlowHookCache.Get(type, progressTracker);
            if (item.Any)
                item.InvokeBeforeDeserialize(instance);

            progressTracker?.OnInstance("Creating " + type.Name, type.Name, type.IsClass, instructionIndex + 1, instructionTotal);
        }
        private void HandleSet(object[] args)
        {
            var field = (string)args[0];
            var rawValue = args[1];

            var instanceID = instanceIDStack.Peek();
            var target = context.GetObject(instanceID)!;

            ReflectionHelper helper = CreateReflectionHelper(ref target, out object actualTarget);

            if (actualTarget is AnonymousTypeReader a)
            {
                // on an anonymous object, the field doesnt exist yet, so we dispatch it
                Dispatch(instanceID, obj =>
                {
                    ReflectionHelper rh = CreateReflectionHelper(ref obj, out object? acttar);
                    MemberData member = rh.GetMember(field);
                    SetValue(rawValue, ref acttar, member);
                });
                return;
            }

            MemberData member = helper.GetMember(field);
            progressTracker?.OnField(field, instructionIndex + 1, instructionTotal);
            SetValue(rawValue, ref actualTarget, member);
        }

        private void SetValue(object rawValue, ref object actualTarget, MemberData member)
        {
            object o = actualTarget;
            object? value = GetArgumentValue(rawValue, 1, member.Type, val =>
            {
                if (member.Type.IsArray)
                    val = ((IList)val).GetInternalArray();

                member.SetValue(ref o, val);
            });
            if (value is Dispatched)
                return; // value has been dispatched to be set later

            if (member.Type.IsArray)
                value = ((IList)value).GetInternalArray();


            member.SetValue(ref actualTarget, value);
        }

        private void Dispatch(int refID, Action<object?> method)
        {
            dispatchedReferences.Add(new(refID, instructionIndex, method));
        }
        private record DispatchedReference(int RefID, int lineNum, Action<object?> method);
        private unsafe void HandleCall(string methodName, int argCount)
        {
            var args = new object[argCount];
            for (int i = argCount - 1; i >= 0; i--)
            {
                object arg = args[i] = context.ValueStack.Pop();
                if (arg is StructReference sr)
                    args[i] = sr.Get();
                else
                    args[i] = arg;
            }

            var target = context.ValueStack.Pop();
            AccessFilterCache.Validate(target is Type type ? type : target.GetType(), AccessFilterKind.Blacklist, methodName);

            progressTracker?.OnMethod(target.GetType().Name, methodName);

            object? val = DynamicMethodInvoker.InvokeMethodWithArguments(
                targetType: target is Type t ? t : target.GetType(),
                methodName,
                target: target is Type ? null : target,
                arguments: args);

            if (!val.GetType().IsClass && !val.GetType().IsPrimitive)
            {
                object* ptr = &val;
                var v = new StructReference(ptr);
                context.ValueStack.Push(v);
            }
            else
                context.ValueStack.Push(val);

        }
        private void HandleAddElement(object[] args)
        {
            var collection = listStack.Peek();

            if (collection is ListDefinition list)
            {
                object element = GetArgumentValue(args[0], 0, list.ElementType, o => throw new WinterForgeDifferedException("Differed collection addition is not allowed"));
                if (element is Dispatched)
                    throw new WinterForgeDifferedException("Differed collection addition is not allowed");

                list.Values.Add(element);
            }
            else if (collection is DictionaryDefinition dict)
            {
                object key = GetArgumentValue(args[0], 0, dict.KeyType, o => throw new WinterForgeDifferedException("Differed collection addition is not allowed"));
                if (key is Dispatched)
                    throw new WinterForgeDifferedException("Differed collection addition is not allowed");

                if (args.Length < 2)
                    throw new WinterForgeExecutionException("Dictionary element did not have a value");

                object value = GetArgumentValue(args[1], 0, dict.ValueType, o => throw new WinterForgeDifferedException("Differed collection addition is not allowed"));
                if (value is Dispatched)
                    throw new WinterForgeDifferedException("Differed collection addition is not allowed");

                dict.Values.Add(key, value);
            }

        }
        private object GetArgumentValue(object arg, int argIndex, Type desiredType, Action<object> onDispatch)
        {
            object? value;
            switch (arg)
            {
                case string s when s.StartsWith("_ref("):
                    int refID = ParseRef(s);
                    value = context.GetObject(refID);
                    if (value == null)
                    {
                        Dispatch(refID, onDispatch);
                        return new Dispatched(); // call dispatched for a later created object!
                    }
                    break;

                case string s when s.StartsWith("_stack("):
                    var stackValue = context.ValueStack.Pop();
                    if (stackValue is string ss)
                        value = ParseLiteral(ss, desiredType);
                    else
                        value = stackValue;

                    break;
                case string s when s.StartsWith("_type("):
                    value = ParseTypeLiteral(s);
                    break;
                case string s when s.StartsWith("_str("):
                    value = ParseStringFunc(s);
                    break;
                case object o when CustomValueProviderCache.Get(desiredType, out var provider):
                    if (o is "null")
                        value = provider.OnNull();
                    else
                    {
                        if (current.Args.Length - 1 > argIndex)
                            o = string.Join(' ', current.Args.Skip(argIndex));
                        value = provider._CreateObject(o, this);
                    }
                    break;
                case object s when desiredType.IsEnum:
                    Type enumNumType = Enum.GetUnderlyingType(desiredType);
                    object num = TypeWorker.CastPrimitive(s, enumNumType);
                    value = Enum.ToObject(desiredType, num);
                    break;
                default:
                    value = ParseLiteral(arg, desiredType);
                    break;
            }

            return value;
        }

        private string? ParseStringFunc(string s)
        {
            if (!s.StartsWith("_str(") || !s.EndsWith(")"))
                return null;

            string inner = s[5..^1];
            if (string.IsNullOrWhiteSpace(inner))
                return string.Empty;

            string[] parts = inner.Split(',', StringSplitOptions.RemoveEmptyEntries);
            byte[] bytes = new byte[parts.Length];

            for (int i = 0; i < parts.Length; i++)
            {
                if (!byte.TryParse(parts[i].Trim(), out bytes[i]))
                    return null; // invalid byte value
            }

            return Encoding.UTF8.GetString(bytes);
        }

        private void HandleEnd()
        {
            int currentID = instanceIDStack.Peek();
            object? currentObj = context.GetObject(currentID);

            if (currentObj is AnonymousTypeReader reader)
            {
                string msg = "Compiling anonymous type";
                if (reader.TypeName != null)
                    msg += ": " + reader.TypeName;
                progressTracker?.Report(msg);
                object compiledAnonymous = reader.Compile();
                context.ObjectTable[currentID] = compiledAnonymous;
                currentObj = compiledAnonymous;
            }
            else
            {
                FlowHookItem item = FlowHookCache.Get(currentObj.GetType(), progressTracker);
                if (item.Any)
                    item.InvokeAfterDeserialize(currentObj);
            }

            for (int i = 0; i < dispatchedReferences.Count;)
            {
                DispatchedReference r = dispatchedReferences[i];
                if (r.RefID == currentID)
                {
                    r.method(currentObj);
                    dispatchedReferences.Remove(r);
                }
            }

            instanceIDStack.Pop();
        }
        private static object? ParseLiteral(object o, Type target)
        {
            if (o.GetType() == target)
                return o;
            if(o is string raw)
            {
                if (raw is "null")
                    return null;
                if (target == typeof(string))
                    return raw;
                string r = raw.Replace('.', ',');
                return TypeWorker.CastPrimitive(r, target);
            }
            if (TypeConverter.TryConvert(o, target, out var converted))
                return converted;
            return o;
            
        }
        private static int ParseRef(string raw)
        {
            var inner = raw[5..^1];
            return int.Parse(inner);
        }

        private static Type ParseTypeLiteral(string raw)
        {
            var inner = raw[6..^1];
            return TypeWorker.FindType(inner);
        }

        /// <summary>
        /// Resolves the type from a string that was generated using <see cref="ObjectSerializer.ParseTypeName(Type)"/> back into a <see cref="Type"/> reference
        /// </summary>
        /// <param name="typeName"></param>
        /// <returns></returns>
        public static Type ResolveType(string typeName)
        {
            ValidateKeywordType(ref typeName);

            if (typeCache.TryGetValue(typeName, out Type cachedType))
                return cachedType;

            Type resolvedType;


            if (typeName.Contains('<') && typeName.Contains('>'))
            {
                int startIndex = typeName.IndexOf('<');
                int endIndex = typeName.LastIndexOf('>');

                string baseTypeName = typeName[..startIndex];

                string genericArgsString = typeName.Substring(startIndex + 1, endIndex - startIndex - 1);

                List<string> genericArgs = ParseGenericArguments(genericArgsString);
                if (genericArgs.Count > 0)
                    baseTypeName += "`" + genericArgs.Count.ToString();

                Type baseType = TypeWorker.FindType(baseTypeName);

                Type[] resolvedGenericArgs = genericArgs
                    .Select(arg => ResolveType(arg))
                    .ToArray();

                resolvedType = baseType.MakeGenericType(resolvedGenericArgs);
            }
            else // parse non generic types
            {
                resolvedType = TypeWorker.FindType(typeName);
            }

            typeCache[typeName] = resolvedType;

            return resolvedType;
        }
        private static void ValidateKeywordType(ref string typeName)
        {
            typeName = typeName switch
            {
                "int" => "System.Int32",
                "long" => "System.Int64",
                "short" => "System.Int16",
                "byte" => "System.Byte",
                "bool" => "System.Boolean",
                "float" => "System.Single",
                "double" => "System.Double",
                "decimal" => "System.Decimal",
                "char" => "System.Char",
                "string" => "System.String",
                "object" => "System.Object",
                _ => typeName // assume it's already a CLR type or custom type
            };
        }
        private static List<string> ParseGenericArguments(string args)
        {
            List<string> result = new List<string>();
            int nestingLevel = 0;
            StringBuilder currentArg = new StringBuilder();

            for (int i = 0; i < args.Length; i++)
            {
                char c = args[i];

                if (c == ',' && nestingLevel == 0)
                {
                    result.Add(currentArg.ToString().Trim());
                    currentArg.Clear();
                }
                else
                {
                    if (c == '<') nestingLevel++;

                    if (c == '>') nestingLevel--;

                    currentArg.Append(c);
                }
            }

            if (currentArg.Length > 0)
                result.Add(currentArg.ToString().Trim());

            return result;
        }

        private class Dispatched();
    }


}
