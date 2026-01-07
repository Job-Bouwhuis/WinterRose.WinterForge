using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using WinterRose.AnonymousTypes;
using WinterRose.Reflection;
using WinterRose.WinterForgeSerializing.Compiling;
using WinterRose.WinterForgeSerializing.Containers;
using WinterRose.WinterForgeSerializing.Instructions;
using WinterRose.WinterForgeSerializing.Logging;
using WinterRose.WinterForgeSerializing.Util;

namespace WinterRose.WinterForgeSerializing.Workers;

/// <summary>
/// Used to deserialize a collection of <see cref="Instruction"/>
/// </summary>
public class WinterForgeVM : IDisposable
{
    /// <summary>
    /// Enables some 
    /// </summary>
    public static bool Debug { get; set; } = false;

    /// <summary>
    /// when true, prints the debug stuff to the console automatically. it really is for debug
    /// </summary>
    public static bool DebugAutoPrint { get; set; } = false;

    /// <summary>
    /// The default functions, if their entry here does not contain a closing ) they may accept argument(s)
    /// </summary>
    public static IReadOnlyList<string> BuiltinFunctions =
    [
        "#ref(",
        "#stack()",
        "#type()",
        "#workingDir()",
        "#str("
    ];

    /// <summary>
    /// 
    /// </summary>
    /// <param name="callTicksToWait">The amount of times the VM enumerator has to have its next() called until it goes to the next instruction</param>
    public class OpcodeTiming(int callTicksToWait)
    {
        public int CallTicksToWait { get; set; } = callTicksToWait;
    }

    public FrozenDictionary<OpCode, OpcodeTiming> OpcodeTimings { get; } = Enum.GetValues<OpCode>().Select(op => new KeyValuePair<OpCode, OpcodeTiming>(op, new(0))).ToFrozenDictionary();

    public bool IsDisposed { get; private set; }

    private class Any;

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

    internal WinterForgeProgressTracker? progressTracker;
    private static readonly ConcurrentDictionary<string, Type> typeCache = new();
    private readonly Stack<DeserializationContext> contextStack = new();
    private readonly Stack<int> instanceIDStack;
    private readonly Stack<CollectionDefinition> listStack = new();
    private readonly List<DispatchedReference> dispatchedReferences = [];
    private Stack<Dictionary<string, (int Index, int Depth)>>? labelCache = new();
    private StringBuilder? sb;
    private Instruction current;

    private DeserializationContext CurrentContext
    {
        get
        {
            if (contextStack.Count == 0)
                throw new InvalidOperationException("No deserialization context available.");
            return contextStack.Peek();
        }
    }

    private object? GetObjectFromContexts(int id)
    {
        foreach (var ctx in contextStack)
        {
            // Stack enumerates from top -> bottom (LIFO), which is what we want
            var o = ctx.GetObject(id);
            if (o != null)
                return o;
        }

        return null;
    }

    private bool TryGetContainerFromContexts(string name, out Container container)
    {
        foreach (var ctx in scopeStack)
        {
            if (ctx.Containers.TryGetValue(name, out container))
                return true;
        }

        container = null!;
        return false;
    }

    private int instructionTotal;

    private Dictionary<OpCode, (long totalTicks, int count)> opcodeTimings;
    internal readonly Stack<int> instructionIndexStack = new();

    internal readonly Stack<Scope> scopeStack = new();
    public Scope? CurrentScope => scopeStack.Count > 0 ? scopeStack.Peek() : null;

    public IEnumerator<object> GetExecutor(InstructionStream instructionCollection, bool clearInternals)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        int instructionTotal = instructionCollection.Count;

        try
        {
            instructionIndexStack.Push(0);
            progressTracker?.Report(0);
            progressTracker?.Report("Starting...");

            contextStack.Push(new DeserializationContext());
            labelCache.Push(null);

            Variable? buildingVariable = null;
            int scopesCreated = 0;
            StringBuilder? sb = null;

            void increaseIndex()
            {
                instructionIndexStack.Push(instructionIndexStack.Pop() + 1);
            }

            for (; instructionIndexStack.Peek() < instructionCollection.Count; increaseIndex())
            {
                try
                {
                    current = instructionCollection[instructionIndexStack.Peek()];
                }
                catch (IndexOutOfRangeException)
                {
                    break;
                }

                Instruction instruction = instructionCollection[instructionIndexStack.Peek()];
                long startTicks = 0;

                if (CurrentContext.constructingScopes.TryPeek(out Scope cs)
                    && cs is Template templ
                    && instruction.Opcode is not OpCode.TEMPLATE_CREATE and not OpCode.TEMPLATE_END
                        and not OpCode.CONTAINER_START and not OpCode.CONTAINER_END
                        and not OpCode.VAR_DEF_START
                        and not OpCode.VAR_DEF_END)
                {
                    templ.Instructions.Add(instruction);
                    // apply post-op tick waits for TEMPLATE accumulation as well
                    int ticksToWaitTpl = OpcodeTimings.TryGetValue(instruction.Opcode, out var tplTiming) ? tplTiming.CallTicksToWait : 1;
                    for (int t = 0; t < ticksToWaitTpl; t++) yield return null;
                    continue;
                }

                if (Debug)
                    startTicks = Stopwatch.GetTimestamp();

                switch (instruction.Opcode)
                {
                    case OpCode.LABEL:
                        break; // nop

                    case OpCode.DEFINE:
                        HandleDefine(instruction.Args);
                        break;
                    case OpCode.SET:
                        HandleSet(instruction.Args);
                        break;
                    case OpCode.ANONYMOUS_SET:
                        HandleAnonymousSet(instruction);
                        break;
                    case OpCode.PUSH:
                        HandlePush(instruction);
                        break;
                    case OpCode.START_STR:
                        sb = new StringBuilder();
                        break;
                    case OpCode.STR:
                        if (sb is null)
                            throw new WinterForgeExecutionException(
                                "Missing 'START_STR' instruction before 'STR' operation");
                        sb.AppendLine((string)instruction.Args[0]);
                        break;
                    case OpCode.END_STR:
                        if (sb is null)
                            throw new WinterForgeExecutionException(
                                "Missing 'START_STR' instruction before 'END_STR' operation");
                        CurrentContext.ValueStack.Push(sb.ToString());
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
                            object returnValue = HandleReturn(instruction, contextStack.Count != 1);
                            for (int i = 0; i < scopesCreated; i++)
                                scopeStack.Pop();
                            // yield the return value and finish
                            yield return returnValue;
                            yield break;
                        }
                    case OpCode.PROGRESS:
                        progressTracker?.Report((this.instructionIndexStack.Peek() + 1) / (float)instructionTotal);
                        break;
                    case OpCode.ACCESS:
                        {
                            HandleAccess(instruction);
                            break;
                        }
                    case OpCode.SETACCESS:
                        HandleSetAccess(instruction);
                        break;
                    case OpCode.AS:
                        CurrentContext.MoveStackTo((int)instruction.Args[0]);
                        break;
                    case OpCode.IMPORT:
                        HandleImport(instruction);
                        break;
                    case OpCode.CREATE_REF:
                        CurrentContext.AddObject((int)instruction.Args[0], ref instruction.Args[1]);
                        break;

                    case OpCode.JUMP:
                        HandleJump((string)instruction.Args[0], instructionCollection);
                        break;
                    case OpCode.JUMP_IF_FALSE:
                        if (!PopBool())
                            HandleJump((string)instruction.Args[0], instructionCollection);
                        break;

                    case OpCode.ADD:
                    case OpCode.SUB:
                    case OpCode.MUL:
                    case OpCode.DIV:
                    case OpCode.MOD:
                    case OpCode.POW:
                        HandleMathOp(instruction);
                        break;

                    case OpCode.NEG:
                        HandleNegatorOp();
                        break;

                    case OpCode.EQ:
                    case OpCode.NEQ:
                    case OpCode.GT:
                    case OpCode.LT:
                    case OpCode.GTE:
                    case OpCode.LTE:
                        HandleCrossValueBooleanOp(instruction);
                        break;

                    case OpCode.AND:
                    case OpCode.OR:
                    case OpCode.XOR:
                        HandleBasicBoolToBoolOp(instruction);
                        break;

                    case OpCode.NOT:
                        {
                            bool value = PopBool();
                            CurrentContext.ValueStack.Push(!value);
                            break;
                        }

                    case OpCode.CONSTRUCTOR_START:
                    case OpCode.TEMPLATE_CREATE:
                        CreateTemplate(instruction);
                        break;

                    case OpCode.TEMPLATE_END: // 38
                        TemplateEnd();
                        break;


                    case OpCode.CONTAINER_START: // 39
                        CreateContainer(instruction);
                        break;

                    case OpCode.CONTAINER_END: // 40
                        FinalizeContainer();
                        break;

                    case OpCode.CONSTRUCTOR_END: // 42
                        {
                            EndConstructor();
                        }
                        break;

                    case OpCode.VAR_DEF_START:
                        (bool flowControl, (buildingVariable, instruction)) = DefineVariable(instruction);
                        if (!flowControl)
                            break;

                        break;

                    case OpCode.VAR_DEF_END: // 44
                        buildingVariable = EndVariable(buildingVariable);
                        break;

                    case OpCode.FORCE_DEF_VAR: // 45
                        {
                            Scope current = scopeStack.Peek();
                            current.DefineVariable(new Variable((string)instruction.Args[0]));
                        }
                        break;

                    case OpCode.SCOPE_PUSH:
                        scopesCreated++;
                        scopeStack.Push(new Container("Scope_" + UniqueRandomVarNameGenerator.Next)
                        {
                            Parent = scopeStack.Peek()
                        });
                        break;

                    case OpCode.SCOPE_POP:
                        scopesCreated--;
                        scopeStack.Pop();
                        break;

                    case OpCode.VOID_STACK_ITEM:
                        CurrentContext.ValueStack.Pop();
                        break;

                    default:
                        throw new WinterForgeExecutionException($"Opcode: {instruction.Opcode} not supported");
                }

                if (Debug)
                {
                    if (instruction.Opcode is OpCode.IMPORT or OpCode.CALL)
                    {
                        // do not time import/call here
                    }
                    else
                    {
                        long endTicks = Stopwatch.GetTimestamp();
                        long elapsedTicks = endTicks - startTicks;

                        if (!(opcodeTimings ??= []).TryGetValue(instruction.Opcode, out var timing))
                            timing = (0, 0);
                        opcodeTimings[instruction.Opcode] = (timing.totalTicks + elapsedTicks, timing.count + 1);
                    }
                }

                // after executing the instruction, yield null the configured number of times
                int ticksToWait = OpcodeTimings.TryGetValue(instruction.Opcode, out var opTiming) ? Math.Max(1, opTiming.CallTicksToWait) : 1;
                for (int t = 0; t < ticksToWait; t++)
                    yield return null;
            }

            // finished without an explicit RET -> return the final Nothing value
            yield return new Nothing(contextStack.SelectMany(stack => stack.ObjectTable).ToDictionary());
        }
        finally
        {
            progressTracker?.Report(1);
            progressTracker?.Report("Finishing up");
            instructionIndexStack.Pop();
            contextStack.Pop();
            labelCache.Pop();

            if (clearInternals)
            {
                while (scopeStack.Peek().Name != "global")
                    scopeStack.Pop();
                while (contextStack.Count > 0)
                    contextStack.Pop().Dispose();
                dispatchedReferences.Clear();
                listStack.Clear();
                instanceIDStack.Clear();
            }

            if (DebugAutoPrint)
                PrintOpcodeTimings(new TextWriterStream(Console.Out));
        }

        bool PeekInstruction([NotNullWhen(true)] out Instruction instruction)
        {
            instruction = default;
            if (instructionIndexStack.Peek() + 1 >= instructionCollection.Count)
                return false;
            instruction = instructionCollection[instructionIndexStack.Peek() + 1];
            return true;
        }

        bool NextInstruction([NotNullWhen(true)] out Instruction instruction)
        {
            instruction = default;
            if (instructionIndexStack.Peek() + 1 >= instructionCollection.Count)
                return false;
            instructionIndexStack.Push(instructionIndexStack.Pop() + 1);
            instruction = instructionCollection[instructionIndexStack.Peek()];
            return true;
        }

        (bool flowControl, (Variable? buildingVariable, Instruction instruction) value) DefineVariable(Instruction instruction)
        {
            Variable? buildingVariableLocal;
            if (WinterForge.AllowedScriptingLevel < WinterForge.ScriptingLevel.All)
                throw new WinterForgeExecutionException(
                    "Illegal variable declaration: container-scope variables are not permitted at the current scripting level"
                );
            buildingVariableLocal = new Variable();
            buildingVariableLocal.Name = (string)instruction.Args[0];

            if (instruction.Args.Length == 1)
            {
                if (PeekInstruction(out Instruction instr)
                    && instr.Opcode == OpCode.VAR_DEF_END)
                {
                    buildingVariableLocal.defaultValue = null;
                    buildingVariableLocal.DefaultValueAsExpression = false;
                    return (flowControl: false, value: default);
                }

                goto ExpressionBuilding;
            }

            if (instruction.Args.Length == 2)
            {
                buildingVariableLocal.defaultValue = instruction.Args[1];
                buildingVariableLocal.DefaultValueAsExpression = false;
                return (flowControl: false, value: default);
            }

ExpressionBuilding:
            buildingVariableLocal.DefaultValueInstructions = [];
            buildingVariableLocal.DefaultValueAsExpression = true;
            while (NextInstruction(out instruction))
            {
                if (instruction.Opcode == OpCode.VAR_DEF_END)
                    EndVariable(buildingVariableLocal);
                buildingVariableLocal.DefaultValueInstructions.Add(instruction);
            }

            return (flowControl: true, value: default);
        }
    }

    public object? Execute(InstructionStream instructionCollection, bool clearInternals)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        object? result = null;
        // consume executor until it yields a non-null object (the RET value or final Nothing)
        foreach (var tick in GetExecutor(instructionCollection, clearInternals))
        {
            if (tick != null)
            {
                result = tick;
                break;
            }
        }

        return result;
    }


    private Variable? EndVariable(Variable? buildingVariable)
    {
        if (WinterForge.AllowedScriptingLevel < WinterForge.ScriptingLevel.All)
            throw new WinterForgeExecutionException(
                "Illegal variable declaration: container-scope variables are not permitted at the current scripting level"
            );
        {
            Scope s = CurrentContext.constructingScopes.Peek();
            if (s is null)
                throw new WinterForgeExecutionException(
                    "Attempting to add a variable on a non existing scope is impossible!");

            if (buildingVariable is null)
                throw new WinterForgeExecutionException(
                    "Attempting to add a null variable to a scope is illegal!");

            s.DefineVariable(buildingVariable);
            buildingVariable = null;
        }

        return buildingVariable;
    }

    private void EndConstructor()
    {
        if (!CurrentContext.constructingScopes.TryPop(out Scope s) || s is not Template t)
            throw new WinterForgeExecutionException(
                "Tried ending a constructor but most recent scope was not a template");

        if (!CurrentContext.constructingScopes.TryPeek(out s) || s is not Container c)
            throw new WinterForgeExecutionException(
                "Tried ending a constructor but just ended template was not defined in a container");

        t.Parent = c;
        c.Constructors.DefineTemplate(t);
    }

    private void FinalizeContainer()
    {
        if (WinterForge.AllowedScriptingLevel < WinterForge.ScriptingLevel.All)
            throw new WinterForgeExecutionException(
                "Illegal container definition: class declarations are not permitted at the current scripting level"
            );
        if (!CurrentContext.constructingScopes.TryPop(out Scope s) || s is not Container c)
            throw new WinterForgeExecutionException(
                "Tried ending a container but just ended scope was not a container");
        scopeStack.Pop();

        CurrentContext.constructingScopes.TryPeek(out Scope owner);
        owner ??= scopeStack.Peek();
        owner.Containers.Add(c.Name, c);
    }

    private void CreateContainer(Instruction instruction)
    {
        if (WinterForge.AllowedScriptingLevel < WinterForge.ScriptingLevel.All)
            throw new WinterForgeExecutionException(
                "Illegal container definition: class declarations are not permitted at the current scripting level"
            );


        Container newContainer = new((string)instruction.Args[0]);
        CurrentContext.constructingScopes.Push(newContainer);
        scopeStack.Push(newContainer);
    }

    private bool TemplateEnd()
    {
        if (WinterForge.AllowedScriptingLevel < WinterForge.ScriptingLevel.All)
            throw new WinterForgeExecutionException(
                "Function definition not permitted. Required scripting level exceeds current VM configuration"
            );
        if (!CurrentContext.constructingScopes.TryPop(out Scope s) || s is not Template t)
            throw new WinterForgeExecutionException(
                "Tried ending a template but most recent scope was not a template");

        if (CurrentContext.constructingScopes.Count == 0)
            if (scopeStack.TryPeek(out var current))
            {
                current.DefineTemplate(t);
                return false;
            }

        if (!CurrentContext.constructingScopes.TryPeek(out s))
            throw new WinterForgeExecutionException(
                "Tried ending a template but just ended scope was not defined in a scope");

        s.DefineTemplate(t);
        return true;
    }

    private void CreateTemplate(Instruction instruction)
    {
        if (WinterForge.AllowedScriptingLevel < WinterForge.ScriptingLevel.All)
            throw new WinterForgeExecutionException(
                "Function definition not permitted. Required scripting level exceeds current VM configuration"
            );
        bool isConstructor = CurrentContext.constructingScopes.TryPeek(out Scope s)
                             && s.Name == (string)instruction.Args[0];

        int argCount = (int)instruction.Args[1];

        // when argCount == 0 we skip only name, otherwise name+count
        int startIndex = (argCount == 0) ? 1 : 2;

        List<TemplateParmeter> templateArgs = new(argCount);

        for (int i = startIndex; i < startIndex + argCount * 2; i += 2)
        {
            templateArgs.Add(new TemplateParmeter(
                ResolveType((string)instruction.Args[i]),
                (string)instruction.Args[i + 1]
            ));
        }

        Template t = new Template((string)instruction.Args[0], templateArgs);
        CurrentContext.constructingScopes.Push(t);
    }

    private void HandleJump(string label, IReadOnlyList<Instruction> instructionCollection)
    {
        if (WinterForge.AllowedScriptingLevel < WinterForge.ScriptingLevel.Conditions)
            throw new WinterForgeExecutionException(
                "Illegal control-flow instruction: conditions are disabled by the current VM scripting level");

        if (labelCache!.Peek() is null)
        {
            labelCache.Pop();
            labelCache.Push([]);

            int depth = 1;
            for (int i = 0; i < instructionCollection.Count; i++)
            {
                var instr = instructionCollection[i];

                switch (instr.Opcode)
                {
                    case OpCode.SCOPE_PUSH:
                    case OpCode.CONTAINER_START:
                        depth++;
                        break;

                    case OpCode.SCOPE_POP:
                    case OpCode.CONTAINER_END:
                        depth = Math.Max(0, depth - 1);
                        break;
                }

                if (instr.Opcode == OpCode.LABEL)
                {
                    string name = (string)instr.Args[0];
                    if (!labelCache.Peek().ContainsKey(name))
                        labelCache.Peek()[name] = (i, depth);
                }
            }
        }

        if (!labelCache.Peek().TryGetValue(label, out var target))
            throw new WinterForgeExecutionException($"Unknown label '{label}'");

        int targetIndex = target.Index;
        int targetDepth = target.Depth;

        // unwind runtime scopes until we match the target label's depth
        int currentDepth = scopeStack.Count;
        while (currentDepth > targetDepth)
        {
            // mirror the behavior of OpCode.SCOPE_POP / CONTAINER_END cleanup
            scopeStack.Pop();
            currentDepth--;
        }

        if (currentDepth < targetDepth)
            throw new WinterForgeExecutionException(
                $"Attempted to jump into a deeper scope ('{label}'). Jumps into deeper scope are not allowed.");

        // set next instruction index to the target
        instructionIndexStack.Pop();
        instructionIndexStack.Push(targetIndex);
    }


    // --- disposable scope pusher ---
    public IDisposable PushScope(Scope scope)
    {
        scopeStack.Push(scope);
        return new ScopePusher(this);
    }

    /// <summary>
    /// Create a new instance of the <see cref="WinterForgeVM"/> to deserialize objects
    /// </summary>
    public WinterForgeVM()
    {
        instanceIDStack = new Stack<int>();

        scopeStack.Push(new Container("global"));
    }

    /// <summary>
    /// Releases the resources of the VM
    /// </summary>
    public void Dispose()
    {
        if (IsDisposed)
            return;
        IsDisposed = true;

        while (contextStack.Count > 0)
        {
            contextStack.Pop().Dispose();
        }

        dispatchedReferences.Clear();
        listStack.Clear();
        instanceIDStack.Clear();
    }

    private object? HandleReturn(Instruction instruction, bool isBody)
    {
        ValidateReturn();

        var arg0 = instruction.Args[0];
        object returnVal = GetArgumentValue(arg0, 0, typeof(Any));

        if (arg0 is string arg && returnVal is string ret && ret == arg)
        {
            if (arg.All(char.IsLetter))
            {
                throw new WinterForgeExecutionException($"Identifier {arg} not found!");
            }
        }

        if (!isBody)
        {
            if (returnVal is int parsedId && instruction.Args[0] is string s && !s.StartsWith("#ref("))
            {
                returnVal = GetObjectFromContexts(parsedId)
                            ?? throw new Exception($"object with ID {parsedId} not found");
            }
        }

        if (returnVal is Dispatched)
            throw new WinterForgeExecutionException("Can not dispatch a return value!");
        return returnVal;
    }

    private void HandleAccess(Instruction instruction)
    {
        string fieldName = (string)instruction.Args[0];
        object o = CurrentContext.ValueStack.Pop();

        // If the target is a Scope (Container/Scope-derived), prefer scope identifier lookup first.
        if (o is Scope scopeTarget)
        {
            if (scopeTarget is Template)
                throw new WinterForgeExecutionException("Tried accessing on a template. This is illegal!");

            var id = scopeTarget.GetIdentifier(fieldName);
            if (id is Variable v)
            {
                CurrentContext.ValueStack.Push(v.defaultValue);
                return;
            }

            if (id is TemplateGroup tg)
            {
                CurrentContext.ValueStack.Push(tg);
                return;
            }

            throw new WinterForgeExecutionException(
                $"Variable or Template with name {fieldName} does not exist on the container!");
        }

        AccessFilterCache.Validate(o is Type ? (Type)o : o.GetType(), AccessFilterKind.Blacklist, fieldName);
        ReflectionHelper rh = CreateReflectionHelper(ref o, out _);
        CurrentContext.ValueStack.Push(rh.GetValueFrom(fieldName));
    }

    private void HandleImport(Instruction instruction)
    {
        string fileName = (string)instruction.Args[0];
        if (fileName.StartsWith('"') && fileName.EndsWith('"'))
            fileName = fileName[1..^1];

        FileInfo? fileInf = new DirectoryInfo(WinterForge.ImportDir).GetFiles($"{fileName}.*").FirstOrDefault();
        if (fileInf is null)
            throw new WinterForgeExecutionException(
                $"Import for file {fileName} can not be found in the WinterForge import directory: {WinterForge.ImportDir}");
        using FileStream stream = fileInf.OpenRead();
        var instr = ByteToOpcodeDecompiler.Parse(stream);
        var VM = new WinterForgeVM();

        object? val = VM.Execute(instr, true);

        int id = TypeConverter.Convert<int>(instruction.Args[1]);
        CurrentContext.AddObject(id, ref val);
        if (val is Container c && !c.isInstance)
            scopeStack.Peek().Containers[c.Name] = c;

        if (Debug)
        {
            var timings = VM.opcodeTimings;
            foreach (var timing in timings)
            {
                if (!(opcodeTimings ??= []).TryGetValue(timing.Key, out var t))
                    t = (0, 0);

                (long ticks, int count) = timing.Value;
                t.totalTicks += ticks;
                t.count += count;

                opcodeTimings[timing.Key] = t;
            }
        }
    }

    #region benchmark methods

    public void PrintOpcodeTimings(Stream outputStream)
    {
        using var writer = new StreamWriter(outputStream, leaveOpen: true);

        var averages = GetAverageOpcodeTimes(); // Dictionary<OpCode, double>

        if (averages.Count == 0)
        {
            writer.WriteLine("No opcode timing data available.");
            writer.Flush();
            return;
        }

        writer.WriteLine("Opcode Execution Counts and Average Times (ms):");
        writer.WriteLine("------------------------------------------------");

        foreach (var opcode in averages.Keys)
        {
            string opcodeName = opcode.ToString();
            int count = opcodeTimings[opcode].count;
            double avgMs = averages[opcode];
            writer.WriteLine($"{opcodeName,-15} : {count,7} executions, {avgMs,8:F4} ms avg");
        }

        writer.WriteLine("------------------------------------------------");
        writer.WriteLine($"Distinct opcodes timed: {averages.Count}");
        writer.Flush();
    }

    public Dictionary<OpCode, double> GetAverageOpcodeTimes()
    {
        if (!Debug)
            return [];
        var result = new Dictionary<OpCode, double>();
        double tickFrequency = Stopwatch.Frequency; // ticks per second

        foreach (var kvp in opcodeTimings)
        {
            double avgTicks = (double)kvp.Value.totalTicks / kvp.Value.count;
            double avgMs = (avgTicks / tickFrequency) * 1000;
            result[kvp.Key] = avgMs;
        }

        return result;
    }

    #endregion

    private void HandleMathOp(Instruction instruction)
    {
        (decimal a, decimal b) = PopTwoDecimals();

        decimal result = instruction.Opcode switch
        {
            OpCode.ADD => a + b,
            OpCode.SUB => a - b,
            OpCode.MUL => a * b,
            OpCode.DIV => b == 0 ? throw new WinterForgeExecutionException("Division by zero") : a / b,
            OpCode.MOD => b == 0 ? throw new WinterForgeExecutionException("Modulo by zero") : a % b,
            OpCode.POW => (decimal)Math.Pow((double)a, (double)b),
            _ => throw new InvalidOperationException($"Unsupported arithmetic opcode: {instruction.Opcode}")
        };

        CurrentContext.ValueStack.Push(result);
    }

    private void HandleNegatorOp()
    {
        object value = CurrentContext.ValueStack.Pop();
        value = GetArgumentValue(value, 0, typeof(decimal), null);
        if (value is Dispatched)
            throw new WinterForgeExecutionException("Cant differ usage of in-expression value");

        CurrentContext.ValueStack.Push(-(decimal)value);
    }

    private void HandleCrossValueBooleanOp(Instruction instruction)
    {
        object b = CurrentContext.ValueStack.Pop();
        object a = CurrentContext.ValueStack.Pop();

        bool result = instruction.Opcode switch
        {
            OpCode.EQ => AreEqual(a, b),
            OpCode.NEQ => !AreEqual(a, b),
            OpCode.GT => CompareObjs(a, b) > 0,
            OpCode.LT => CompareObjs(a, b) < 0,
            OpCode.GTE => CompareObjs(a, b) >= 0,
            OpCode.LTE => CompareObjs(a, b) <= 0,
            _ => throw new InvalidOperationException($"Unsupported comparison opcode: {instruction.Opcode}")
        };

        CurrentContext.ValueStack.Push(result);
    }

    private static bool AreEqual(object? a, object? b)
    {
        // Handle null cases first
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;

        // If same type, just compare directly
        if (a.GetType() == b.GetType())
            return a.Equals(b);

        // Try numeric comparison if both can be numbers
        if (TryConvertToDecimal(a, out decimal da) && TryConvertToDecimal(b, out decimal db))
            return da == db;

        // Try string comparison as a fallback
        return string.Equals(a.ToString(), b.ToString(), StringComparison.Ordinal);
    }

    private static int CompareObjs(object? a, object? b)
    {
        // Null cases: treat null as less than anything else
        if (a == null && b == null) return 0;
        if (a == null) return -1;
        if (b == null) return 1;

        // If both are same type and IComparable, just compare directly
        if (a.GetType() == b.GetType() && a is IComparable comparableA)
            return comparableA.CompareTo(b);

        // If both can be converted to decimal, compare numerically
        if (TryConvertToDecimal(a, out decimal da) && TryConvertToDecimal(b, out decimal db))
            return da.CompareTo(db);

        // Fall back to string comparison
        return string.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal);
    }

    private static bool TryConvertToDecimal(object obj, out decimal result)
    {
        try
        {
            // Convert.ToDecimal handles int, double, float, string numbers, etc.
            result = Convert.ToDecimal(obj);
            return true;
        }
        catch
        {
            result = 0;
            return false;
        }
    }

    private void HandleBasicBoolToBoolOp(Instruction instruction)
    {
        bool b = PopBool();
        bool a = PopBool();

        bool result = instruction.Opcode switch
        {
            OpCode.AND => a && b,
            OpCode.OR => a || b,
            OpCode.XOR => a ^ b,
            _ => throw new InvalidOperationException($"Unsupported boolean opcode: {instruction.Opcode}")
        };

        CurrentContext.ValueStack.Push(result);
    }

    private void HandleSetAccess(Instruction instruction)
    {
        var field = (string)instruction.Args[0];
        var rawValue = instruction.Args[1];

        var target = CurrentContext.ValueStack.Pop();

        // If the target is a Scope, prefer scope identifier setting.
        if (target is Scope scopeTarget)
        {
            var id = scopeTarget.GetIdentifier(field);
            if (id is Variable v)
            {
                object? va = GetArgumentValue(rawValue, 1, typeof(Any));

                if (va is Dispatched)
                    throw new WinterForgeExecutionException("Async scripting has not (yet) been considered!");

                v.Value = va;
                return;
            }
        }

        AccessFilterCache.Validate(target is Type ? (Type)target : target.GetType(), AccessFilterKind.Blacklist, field);

        var helper = CreateReflectionHelper(ref target, out object actual);

        MemberData member = helper.GetMember(field);

        object? value = GetArgumentValue(rawValue, 1, member.Type, val =>
        {
            if (member.Type.IsArray)
                val = ((IList)val).GetInternalArray();

            member.SetValue(ref actual, val);
        });
        if (value is Dispatched)
            return; // value has been dispatched to be set later
        if (value is StructReference sr)
            value = sr.Get();
        if (member.Type.IsArray)
            value = ((IList)value).GetInternalArray();

        member.SetValue(ref actual, value);
    }

    private unsafe void HandlePush(Instruction instruction)
    {
        object arg = instruction.Args[0];

        if (arg is string)
            arg = GetArgumentValue(arg, 0, typeof(string), delegate { });
        if (arg is Dispatched)
            throw new Exception("Other opcodes rely on PUSH having pushed its value, cant differ reference till later");

        Type t = arg?.GetType() ?? typeof(object);
        if (!t.IsClass && !t.IsPrimitive && t != typeof(decimal))
        {
            object* ptr = &arg;
            var v = new StructReference(ptr);
            CurrentContext.ValueStack.Push(v);
        }
        else
            CurrentContext.ValueStack.Push(arg);
    }

    private void HandleAnonymousSet(Instruction instruction)
    {
        if (CurrentContext.GetObject(instanceIDStack.Peek()) is not AnonymousTypeReader obj)
            throw new InvalidOperationException("Anonymous type reader not found on stack for anonymous set operation");

        progressTracker?.OnField((string)instruction.Args[1], instructionIndexStack.Peek() + 1, instructionTotal);

        Type fieldType = ResolveType((string)instruction.Args[0]);

        object val = GetArgumentValue(instruction.Args[2], 2, fieldType, val => { });
        obj.SetMember((string)instruction.Args[1], ref val);
    }

    (decimal, decimal) PopTwoDecimals()
    {
        object b = CurrentContext.ValueStack.Pop();
        object a = CurrentContext.ValueStack.Pop();

        if (a is not decimal and IConvertible)
        {
            a = Convert.ChangeType(a, typeof(decimal));
        }
        else
            a = GetArgumentValue(a, 0, typeof(decimal), null);

        if (a is Dispatched) throw new WinterForgeExecutionException("Cant differ usage of in-expression value");

        if (b is not decimal and IConvertible)
        {
            b = Convert.ChangeType(b, typeof(decimal));
        }
        else
            b = GetArgumentValue(b, 0, typeof(decimal), null);

        if (b is Dispatched) throw new WinterForgeExecutionException("Cant differ usage of in-expression value");

        return ((decimal)a, (decimal)b);
    }

    bool PopBool()
    {
        object value = CurrentContext.ValueStack.Pop();
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

    private void ValidateReturn()
    {
        if (dispatchedReferences.Count > 0)
        {
            StringBuilder sb = new("\nOn lines:\n");
            foreach (DispatchedReference d in dispatchedReferences)
            {
                try
                {
                    object? obj = GetObjectFromContexts(d.RefID);
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

            throw new Exception("There were objects referenced in the deserialization that were never defined." +
                                sb.ToString());
        }
    }

    private void HandleEndList()
    {
        ICollection list = listStack.Pop().Collection;
        CurrentContext.ValueStack.Push(list);
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

    private void HandleDefine(object[] args)
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

            CurrentContext.AddObject(id, ref o);
            instanceIDStack.Push(id);
            string tn = obj.TypeName ?? "Anonymous";
            progressTracker?.OnInstance("Creating " + tn, tn, true, instructionIndexStack.Peek() + 1, instructionTotal);
            return;
        }

        int numArgs = (int)args[2];

        List<object> constrArgs = [];
        for (int i = numArgs - 1; i >= 0; i--)
            constrArgs.Add(CurrentContext.ValueStack.Pop());

        if (TryGetContainerFromContexts(typeName, out Container singleton))
        {
            Container inst = (Container)singleton.DeepCopy(scopeStack.Peek());

            instanceIDStack.Push(id);
            CurrentContext.ObjectTable[id] = inst;

            if (!inst.CreateInstance(constrArgs, this))
                throw new WinterForgeExecutionException(
                    $"Constructor overload for args {string.Join(", ", constrArgs.Select(x => x.ToString()))} doesnt exist on container {inst.Name}");
            return;
        }


        Type type = ResolveType(typeName);
        if (type is null)
            throw new WinterForgeExecutionException(
                $"Type with name '{typeName}' does not exist either as C# type, or (imported) container. Cant create instance.");


        object instance = DynamicObjectCreator.CreateInstance(type, constrArgs)!;
        CurrentContext.AddObject(id, ref instance);

        instanceIDStack.Push(id);

        FlowHookItem item = FlowHookCache.Get(type, progressTracker);
        if (item.Any)
            item.InvokeBeforeDeserialize(instance);

        progressTracker?.OnInstance("Creating " + type.Name, type.Name, type.IsClass, instructionIndexStack.Peek() + 1,
            instructionTotal);
    }

    private void HandleSet(object[] args)
    {
        var field = (string)args[0];
        var rawValue = args[1];
        if (field == "Name")
            ;
        // --- Determine target object or global scope ---
        object? target = null;
        int? instanceID = instanceIDStack.Count > 0 ? instanceIDStack.Peek() : null;
        if (instanceID.HasValue)
            target = GetObjectFromContexts(instanceID.Value);

        // --- If no instance available, use latest scope as target ---
        if (target == null && scopeStack.Count > 0)
            target = scopeStack.Peek();

        // --- Scope variable setting first ---
        if (target is Scope scopeTarget)
        {
            var id = scopeTarget.GetIdentifier(field);
            if (id is Variable v)
            {
                object? value = GetArgumentValue(rawValue, 1, typeof(Any));
                if (value is Dispatched)
                    throw new WinterForgeExecutionException("Async scripting has not (yet) been considered!");
                v.Value = value;
                return;
            }
        }

        // --- Reflection fallback for fields ---
        if (target != null)
        {
            ReflectionHelper helper = CreateReflectionHelper(ref target, out object actualTarget);

            if (actualTarget is AnonymousTypeReader a)
            {
                Dispatch(instanceID ?? -1, obj =>
                {
                    ReflectionHelper rh = CreateReflectionHelper(ref obj, out object? acttar);
                    MemberData member = rh.GetMember(field);
                    SetValue(rawValue, ref acttar, member);
                });
                return;
            }

            MemberData member = helper.GetMember(field);
            if (member.IsValid)
            {
                progressTracker?.OnField(field, instructionIndexStack.Peek() + 1, instructionTotal);
                SetValue(rawValue, ref actualTarget, member);
                return;
            }
        }

        // --- Final fallback: define or update variable in latest scope ---
        if (scopeStack.Count > 0)
        {
            var latestScope = scopeStack.Peek();
            var id = latestScope.GetIdentifier(field) as Variable;
            if (id == null)
            {
                // variable didn't exist, force-create it
                id = new Variable(field);
                latestScope.DefineVariable(id);
            }

            id.Value = GetArgumentValue(rawValue, 1, typeof(object));
        }
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

        if (member.Type.IsArray && value is IList l && !value.GetType().IsArray)
            value = l.GetInternalArray();


        member.SetValue(ref actualTarget, value);
    }

    private void Dispatch(int refID, Action<object?> method)
    {
        dispatchedReferences.Add(new(refID, instructionIndexStack.Peek(), method));
    }

    private record DispatchedReference(int RefID, int lineNum, Action<object?> method);

    private unsafe void HandleCall(string methodName, int argCount)
    {
        var args = new object[argCount];
        for (int i = argCount - 1; i >= 0; i--)
        {
            object arg = args[i] = CurrentContext.ValueStack.Pop();
            if (arg is StructReference sr)
                args[i] = sr.Get();
            else
                args[i] = arg;
        }


        object target = null;
        if (CurrentContext.ValueStack.Count > 0)
            target = CurrentContext.ValueStack.Pop();

        // If the target is a Scope (container), try resolving templates on that scope first.
        if (target is Scope scopeTarget)
        {
            object? returnVal;
            var id = scopeTarget.GetIdentifier(methodName);
            if (id is TemplateGroup tg)
            {
                if (tg.TryCall(out returnVal, args.ToList(), this))
                {
                    CurrentContext.ValueStack.Push(returnVal);
                    return;
                }
            }

            if (id is Variable v && v.Value is TemplateGroup g)
            {
                if (g.TryCall(out returnVal, args.ToList(), this))
                {
                    CurrentContext.ValueStack.Push(returnVal);
                    return;
                }
            }
        }

        // Also allow calling templates by name from the current execution scope (standalone calls).
        if (CurrentScope is not null)
        {
            var id = CurrentScope.GetIdentifier(methodName);
            if (id is Variable v && v.Value is TemplateGroup tmplsg)
            {
                id = tmplsg;
            }

            if (id is TemplateGroup tg)
            {
                if (tg.TryCall(out object returnVal, args.ToList(), this))
                {
                    // the target was not the call target, reinstigate it on the stack before the actual return value
                    if (target is not null)
                        CurrentContext.ValueStack.Push(target);
                    CurrentContext.ValueStack.Push(returnVal);
                    return;
                }
            }
        }

        // we check here if the reference is a template.
        // since a template reference may be stored in a global variable (thus using #ref() accessing)
        if (methodName.StartsWith("#ref(")
            && GetObjectFromContexts(int.Parse(methodName[5..^1])) is TemplateGroup templg)
        {
            if (templg.TryCall(out object returnVal, args.ToList(), this))
            {
                // the target was not the call target, reinstigate it on the stack before the actual return value
                if (target is not null)
                    CurrentContext.ValueStack.Push(target);
                CurrentContext.ValueStack.Push(returnVal);
                return;
            }
        }

        AccessFilterCache.Validate(target is Type type ? type : target.GetType(), AccessFilterKind.Blacklist,
            methodName);

        progressTracker?.OnMethod(target.GetType().Name, methodName);

        object? val = DynamicMethodInvoker.InvokeMethodWithArguments(
            targetType: target is Type t ? t : target.GetType(),
            methodName,
            target: target is Type ? null : target,
            args: [.. args]);

        if (val is Type rett && rett == typeof(void))
            return;

        if (val is not null && !val.GetType().IsClass && !val.GetType().IsPrimitive)
        {
            object* ptr = &val;
            var v = new StructReference(ptr);
            CurrentContext.ValueStack.Push(v);
        }
        else
            CurrentContext.ValueStack.Push(val);
    }

    private void HandleAddElement(object[] args)
    {
        var collection = listStack.Peek();

        if (collection is ListDefinition list)
        {
            object element = GetArgumentValue(args[0], 0, list.ElementType,
                o => throw new WinterForgeDifferedException("Differed collection addition is not allowed"));
            if (element is Dispatched)
                throw new WinterForgeDifferedException("Differed collection addition is not allowed");

            list.Values.Add(element);
        }
        else if (collection is DictionaryDefinition dict)
        {
            object key = GetArgumentValue(args[0], 0, dict.KeyType,
                o => throw new WinterForgeDifferedException("Differed collection addition is not allowed"));
            if (key is Dispatched)
                throw new WinterForgeDifferedException("Differed collection addition is not allowed");

            if (args.Length < 2)
                throw new WinterForgeExecutionException("Dictionary element did not have a value");

            object value = GetArgumentValue(args[1], 0, dict.ValueType,
                o => throw new WinterForgeDifferedException("Differed collection addition is not allowed"));
            if (value is Dispatched)
                throw new WinterForgeDifferedException("Differed collection addition is not allowed");

            dict.Values.Add(key, value);
        }
    }

    private object GetArgumentValue(object arg, int argIndex, Type desiredType, Action<object> onDispatch = null)
    {
        onDispatch ??= delegate { };
        object? value;

        if (arg is string stringArg && stringArg.StartsWith('"') && stringArg.EndsWith('"'))
            return stringArg[1..^1];

        if (arg is string plain && !plain.StartsWith("#") && !plain.StartsWith("_"))
        {
            var id = CurrentScope?.GetIdentifier(plain);
            if (id is Variable v)
                return v.Value!;

            if (id is TemplateGroup tg)
                return tg;

            Container[] containers = scopeStack.Peek().Containers.Values.Where(c => c.Name == plain).ToArray();
            if (containers.Length >= 1)
                return containers[0];
        }

        switch (arg)
        {
            case string s when s.StartsWith("#ref("):
                int refID = ParseRef(s);
                value = GetObjectFromContexts(refID);
                if (value == null)
                {
                    Dispatch(refID, onDispatch);
                    return new Dispatched(); // call dispatched for a later created object!
                }

                break;

            case string s when s.StartsWith("#stack("):
                var stackValue = CurrentContext.ValueStack.Pop();
                if (stackValue is string ss)
                    value = ParseLiteral(ss, desiredType);
                else
                    value = stackValue;

                break;
            case string s when s.StartsWith("#type("):
                value = ParseTypeLiteral(s);
                break;
            case string s when s.StartsWith("#str("):
                value = ParseStringFunc(s);
                break;
            case string s when s.StartsWith("#workingDir("):
                int indexOfOpen = s.IndexOf('(');
                if (s.Length == indexOfOpen)
                    throw new WinterForgeFormatException("Identifier #workingDir did not have a closing ')'");
                bool noArg = s[indexOfOpen + 1] is ')';
                if (noArg)
                {
                    value = WinterForge.WorkingDir;
                    break;
                }

                string newWorkingDir = s["#workingDir(".Length..^1];
                value = WinterForge.WorkingDir = newWorkingDir;
                break;
            case object o when CustomValueProviderCache.Get(desiredType, out var provider):
                if (o is "null")
                    value = provider.OnNull();
                else
                {
                    if (current.Args != null && current.Args.Length - 1 > argIndex)
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
        if (!s.StartsWith("#str(") || !s.EndsWith(")"))
            return null;

        string inner = s[5..^1];
        if (string.IsNullOrWhiteSpace(inner))
            return string.Empty;

        string[] parts = inner.Split(',', StringSplitOptions.RemoveEmptyEntries);
        byte[] bytes = new byte[parts.Length];

        for (int i = 0; i < parts.Length; i++)
        {
            if (!byte.TryParse(parts[i].Trim(), out bytes[i]))
                return null;
        }

        return Encoding.UTF8.GetString(bytes);
    }

    private void HandleEnd()
    {
        int currentID = instanceIDStack.Peek();
        object? currentObj = GetObjectFromContexts(currentID);

        if (currentObj is AnonymousTypeReader reader)
        {
            string msg = "Compiling anonymous type";
            if (reader.TypeName != null)
                msg += ": " + reader.TypeName;
            progressTracker?.Report(msg);
            object compiledAnonymous = reader.Compile();
            CurrentContext.ObjectTable[currentID] = compiledAnonymous;
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

    private object? ParseLiteral(object o, Type target)
    {
        if (o.GetType() == target)
            return o;

        if (target == typeof(object))
            return o;

        if (target == typeof(Any) && o is string s)
        {
            return ParseToAny(s);
        }

        if (o is string raw)
        {
            if (raw is "null")
                return null;
            if (target == typeof(string))
                return raw;
            string r = raw.Replace('.', ',');
            return TypeWorker.CastPrimitive(raw, target);
        }

        if (TypeConverter.TryConvert(o, target, out var converted))
            return converted;
        return o;
    }

    private static object CastValue(object value, Type target)
    {
        if (target == typeof(object))
            return value;
        if (TypeWorker.SupportedPrimitives.Contains(target)
            && TypeWorker.SupportedPrimitives.Contains(value.GetType())
            && target != value.GetType())
            return TypeWorker.CastPrimitive(value, target);
        else if (TypeWorker.FindImplicitConversionMethod(target, value.GetType()) is MethodInfo conversionMethod)
            return conversionMethod.Invoke(null, [value])!;
        else if (TypeConverter.CanConvert(value.GetType(), target))
            return TypeConverter.Convert(value, target);
        return value;
    }

    private object ParseToAny(string raw) => raw switch
    {
        _ when raw.StartsWith("\"") => raw.Trim('"'),
        _ when raw.StartsWith("#ref(") && raw.EndsWith(")") => int.Parse(raw[5..^1]),
        _ when raw.StartsWith("#stack()") => CurrentContext.ValueStack.Pop(),
        _ when raw.StartsWith("#type()") => ParseTypeLiteral(raw),
        _ when raw.StartsWith("#workingDir()") => WinterForge.WorkingDir,
        "default" => 0,
        _ when raw.Count(c => c == '.') > 1 => raw,
        _ when bool.TryParse(raw, out var b) => b,
        _ when byte.TryParse(raw, out var b) => b,
        _ when sbyte.TryParse(raw, out var b) => b,
        _ when short.TryParse(raw, out var b) => b,
        _ when ushort.TryParse(raw, out var b) => b,
        _ when int.TryParse(raw, out var b) => b,
        _ when uint.TryParse(raw, out var b) => b,
        _ when long.TryParse(raw, out var b) => b,
        _ when ulong.TryParse(raw, out var b) => b,
        _ when float.TryParse(raw.Replace('.', ','), out var b) => b,
        _ when double.TryParse(raw.Replace('.', ','), out var b) => b,
        _ when decimal.TryParse(raw.Replace('.', ','), out var b) => b,
        _ when ResolveType(raw) is Type t => t,
        _ => raw // fallback: return original string
    };

    private static int ParseRef(string raw)
    {
        var inner = raw[5..^1];
        return int.Parse(inner);
    }

    private static Type ParseTypeLiteral(string raw)
    {
        var inner = raw[6..^1];
        return ResolveType(inner);
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
        else
        {
            resolvedType = TypeWorker.FindType(typeName);
        }

        if (resolvedType is not null)
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
            _ => typeName
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

public static class ext
{
    extension(IEnumerator<object> e)
    {
        public IEnumerator<object> GetEnumerator() => e;
    }
}