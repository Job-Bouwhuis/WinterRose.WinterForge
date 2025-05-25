using System.Collections.Concurrent;
using System.Reflection;

namespace WinterRose.WinterForgeSerializing
{
    internal class FlowHookItem
    {
        public List<MethodInfo> beforeSerialize = [];
        public List<MethodInfo> beforeDeserialize = [];
        public List<MethodInfo> afterDeserialize = [];
        public bool Any => hasHook;
        private bool hasHook;
        private FlowHookItem() { }

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

        private void DoInvoke(MethodInfo method, object target)
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
                        }
                    }, TaskContinuationOptions.OnlyOnFaulted);
                }
            }
            else
            {
                method.Invoke(target, null);
            }
        }

        internal static FlowHookItem FromType(Type t)
        {
            FlowHookItem item = new();
            MethodInfo[] methods = t.GetMethods(
                BindingFlags.Public |
                BindingFlags.Static |
                BindingFlags.NonPublic |
                BindingFlags.Instance |
                BindingFlags.FlattenHierarchy);

            foreach (var method in methods)
            {
                if (method.GetCustomAttribute<BeforeSerializeAttribute>(true) != null)
                {
                    ValidateMethodSignature(method);
                    item.beforeSerialize.Add(method);
                    item.hasHook = true;
                }

                if (method.GetCustomAttribute<BeforeDeserializeAttribute>(true) != null)
                {
                    ValidateMethodSignature(method);
                    item.beforeDeserialize.Add(method);
                    item.hasHook = true;
                }

                if (method.GetCustomAttribute<AfterDeserializeAttribute>(true) != null)
                {
                    ValidateMethodSignature(method);
                    item.afterDeserialize.Add(method);
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
                    $"Method '{method.DeclaringType?.FullName}.{method.Name}' has invalid signature. " +
                    "Valid signatures are:\n" +
                    "  void Method()\n" +
                    "  Task Method()\n");
            }
        }

    }

    internal static class FlowHookCache
    {
        private static readonly ConcurrentDictionary<Type, FlowHookItem> HOOK_CACHE = [];
        public static FlowHookItem Get(Type t) => HOOK_CACHE.GetOrAdd(t, type => FlowHookItem.FromType(t));
    }
}
