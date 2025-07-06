using System.Collections.Concurrent;
using System.Reflection;
using WinterRose.WinterForgeSerializing.Logging;

namespace WinterRose.WinterForgeSerializing
{
    internal class FlowHookItem
    {
        internal struct FlowMethod
        {
            public MethodInfo method { get; set; }
            public bool IsAsync { get; set; }
            public bool IsAwaited { get; set; }
            public Type ReturnType => method.ReturnType;

            public FlowMethod(MethodInfo method, bool isAsync = false, bool isAwaited = false)
            {
                this.method = method;
                IsAsync = isAsync;
                IsAwaited = isAwaited;
            }

            internal object? Invoke(object target, params object?[]? args) => method.Invoke(target, args);
        }

        internal List<FlowMethod> beforeSerialize = [];
        internal List<FlowMethod> beforeDeserialize = [];
        internal List<FlowMethod> afterDeserialize = [];
        internal bool Any => hasHook;

        private WinterForgeProgressTracker Progress { get; }

        private bool hasHook;
        private FlowHookItem(WinterForgeProgressTracker progress)
        {
            Progress = progress;
        }

        public void InvokeBeforeSerialize(object target)
        {
            foreach(var m in beforeSerialize)
                DoInvoke(m, target);
        }

        public void InvokeBeforeDeserialize(object target)
        {
            foreach (var m in beforeDeserialize)
                DoInvoke(m, target);
        }

        public void InvokeAfterDeserialize(object target)
        {
            foreach (var m in afterDeserialize)
                DoInvoke(m, target);
        }

        private void DoInvoke(FlowMethod method, object target)
        {
            if (method.ReturnType == typeof(Task))
            {
                Task? task = (Task?)method.Invoke(target, null);
                if(task is not null)
                {
                    task.ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            Console.Error.WriteLine(t.Exception);
                            Progress?.OnError(t.Exception, method.method.Name, target.GetType().FullName);
                        }
                    }, TaskContinuationOptions.OnlyOnFaulted);

                    if(method.IsAwaited)
                    {
                        task.GetAwaiter().GetResult();
                    }
                }
            }
            else
            {
                method.Invoke(target, null);
            }
        }

        internal static FlowHookItem FromType(Type t, WinterForgeProgressTracker progress)
        {
            FlowHookItem item = new(progress);
            MethodInfo[] methods = t.GetMethods(
                BindingFlags.Public |
                BindingFlags.Static |
                BindingFlags.NonPublic |
                BindingFlags.Instance |
                BindingFlags.FlattenHierarchy);

            foreach (var method in methods)
            {
                if (method.GetCustomAttribute<BeforeSerializeAttribute>(true) is BeforeSerializeAttribute a)
                {
                    ValidateMethodSignature(method);
                    item.beforeSerialize.Add(new FlowMethod(method, method.ReturnType == typeof(Task), a.IsAwaited));
                    item.hasHook = true;
                }

                if (method.GetCustomAttribute<BeforeDeserializeAttribute>(true) is BeforeDeserializeAttribute b)
                {
                    ValidateMethodSignature(method);
                    item.beforeDeserialize.Add(new FlowMethod(method, method.ReturnType == typeof(Task), b.IsAwaited));
                    item.hasHook = true;
                }

                if (method.GetCustomAttribute<AfterDeserializeAttribute>(true) is AfterDeserializeAttribute c)
                {
                    ValidateMethodSignature(method);
                    item.afterDeserialize.Add(new FlowMethod(method, method.ReturnType == typeof(Task), c.IsAwaited));
                    item.hasHook = true;
                }
            }

            return item;
        }

        private static void ValidateMethodSignature(MethodInfo method)
        {
            var parameters = method.GetParameters();
            var returnType = method.ReturnType;

            //bool isLoggerParam = parameters.Length == 1 && parameters[0].ParameterType == typeof(ILogger);
            bool isValidParams = parameters.Length == 0;// || isLoggerParam;

            bool isVoid = returnType == typeof(void);
            bool isTask = returnType == typeof(Task);

            if (!isValidParams || (!isVoid && !isTask))
            {
                throw new InvalidOperationException(
                    $"Method '{method.DeclaringType?.FullName}.{method.Name}' has invalid signature.\n" +
                    "Valid signatures are:\n" +
                    "  void Method()\n" +
                    "  Task Method()\n");
            }
        }

    }

    internal static class FlowHookCache
    {
        private static readonly ConcurrentDictionary<Type, FlowHookItem> HOOK_CACHE = [];
        public static FlowHookItem Get(Type t, WinterForgeProgressTracker progress) => HOOK_CACHE.GetOrAdd(t, type => FlowHookItem.FromType(t, progress));
    }
}
