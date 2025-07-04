using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace WinterRose.Reflection
{
    /// <summary>
    /// Central registry that maps one Type to another
    /// </summary>
    public static class TypeConverter
    {
        private static readonly ConcurrentDictionary<(Type, Type), ITypeConverter> _cache
            = new();                                                // closed, ready‑to‑use

        private static readonly ConcurrentDictionary<(Type, Type), Type> _genericTemplates
            = new();                                                // open generic “blueprints”

        static TypeConverter() => DiscoverConverters();

        private static bool TryFindPath(Type src, Type tgt, out List<ITypeConverter> path)
        {
            var visited = new HashSet<Type> { src };
            var parents = new Dictionary<Type, (Type Parent, ITypeConverter Edge)>();
            var q = new Queue<Type>();
            q.Enqueue(src);

            while (q.Count > 0)
            {
                var cur = q.Dequeue();

                foreach (var edge in EnumerateConvertersFrom(cur, tgt))
                {
                    var next = edge.TargetType;
                    if (!visited.Add(next)) continue;

                    parents[next] = (cur, edge);

                    if (next == tgt)
                    {
                        var chain = new List<ITypeConverter>();
                        for (Type n = tgt; n != src; n = parents[n].Parent)
                            chain.Add(parents[n].Edge);
                        chain.Reverse();
                        path = chain;
                        return true;
                    }
                    if (next == typeof(byte[]))
                        ;
                    q.Enqueue(next);
                }
            }

            path = null!;
            return false;
        }

        private static IEnumerable<ITypeConverter> EnumerateConvertersFrom(Type src, Type tgt)
        {
            /* 1️⃣ already‑cached exact converters */
            foreach (var kv in _cache)
                if (kv.Key.Item1 == src)
                    yield return kv.Value;

            /* 2️⃣ cached converters for base & interface sources */
            foreach (Type sup in GetAllBaseTypesAndInterfaces(src))
            {
                if (sup == src) continue;
                foreach (var kv in _cache)
                    if (kv.Key.Item1 == sup)
                        yield return kv.Value;
            }

            /* 3️⃣ open‑generic templates that begin with src (or its supertypes) */
            var srcGens = GetAllBaseTypesAndInterfaces(src)
                          .Where(t => t.IsGenericType)
                          .Concat(src.IsGenericType ? new[] { src } : Array.Empty<Type>());

            // cache target‑side facts once
            var tgtIsGen = tgt.IsGenericType;
            var tgtTpl = tgtIsGen ? tgt.GetGenericTypeDefinition() : null;
            var tgtArgs = tgtIsGen ? tgt.GetGenericArguments() : Array.Empty<Type>();

            foreach (var sType in srcGens)
            {
                var sTpl = sType.GetGenericTypeDefinition();
                var sArgs = sType.GetGenericArguments();

                foreach (var (tplKey, openConv) in _genericTemplates)
                {
                    if (tplKey.Item1 != sTpl) continue;

                    var gParams = openConv.GetGenericArguments();
                    Type closed;

                    switch (gParams.Length)
                    {
                        case 1:
                            if (tgtArgs.Length >= 1)
                                closed = openConv.MakeGenericType(tgtArgs[0]);
                            else if (sArgs.Length >= 1)
                                closed = openConv.MakeGenericType(sArgs[0]);
                            else
                                continue;
                            break;

                        case 2:
                            if (tgtArgs.Length >= 2)
                                closed = openConv.MakeGenericType(tgtArgs[0], tgtArgs[1]);
                            else if (tgtArgs.Length == 1)
                                closed = openConv.MakeGenericType(tgtArgs[0], tgtArgs[0]);
                            else if (sArgs.Length >= 2)
                                closed = openConv.MakeGenericType(sArgs[0], sArgs[1]);
                            else if (sArgs.Length == 1)
                                closed = openConv.MakeGenericType(sArgs[0], sArgs[0]);
                            else
                                continue;
                            break;

                        default:
                            continue;
                    }

                    if (!_cache.TryGetValue(
                            (sType, closed.BaseType!.GetGenericArguments()[1]),
                            out var conv))
                    {
                        conv = (ITypeConverter)Activator.CreateInstance(closed)!;
                        _cache.TryAdd((conv.SourceType, conv.TargetType), conv);
                    }
                    yield return conv;
                }
            }
        }


        private static IEnumerable<Type> GetAllBaseTypesAndInterfaces(Type type)
        {
            var allTypes = new List<Type>();

            void Collect(Type? t)
            {
                if (t == null || t == typeof(object)) return;

                allTypes.Add(t);

                foreach (var iface in t.GetInterfaces())
                {
                    if (!allTypes.Contains(iface))
                        allTypes.Add(iface);
                }

                Collect(t.BaseType);
            }

            Collect(type);

            // Order by inheritance depth descending so most derived first
            return allTypes
                .Distinct()
                .OrderByDescending(GetInheritanceDepth);
        }

        private static bool TryGetConverterAssignable(Type src, Type tgt, out ITypeConverter converter)
        {
            // Exact match first
            if (_cache.TryGetValue((src, tgt), out converter))
                return true;

            // Check source interfaces converting to target type
            foreach (Type iface in src.GetInterfaces())
            {
                if (_cache.TryGetValue((iface, tgt), out converter))
                    return true;
            }

            // Check if source and target share any interface, try converter (iface, iface)
            var srcIfaces = src.GetInterfaces();
            var tgtIfaces = tgt.GetInterfaces();

            foreach (Type iface in srcIfaces)
            {
                if (tgtIfaces.Contains(iface) && _cache.TryGetValue((iface, iface), out converter))
                    return true;
            }

            // Search in instantiated converters for assignable matches
            var candidates = _cache.Values.Where(conv =>
                conv.SourceType.IsAssignableFrom(src) && tgt.IsAssignableFrom(conv.TargetType)).ToList();

            // Also consider open generic templates that can be instantiated for src and tgt
            FindConverterCandidates(src, tgt, candidates);

            if (candidates.Count == 0)
            {
                converter = null!;
                return false;
            }

            candidates.Sort((a, b) =>
            {
                int srcDepthA = GetInheritanceDepth(a.SourceType);
                int srcDepthB = GetInheritanceDepth(b.SourceType);
                int tgtDepthA = GetInheritanceDepth(a.TargetType);
                int tgtDepthB = GetInheritanceDepth(b.TargetType);

                int cmp = srcDepthB.CompareTo(srcDepthA);
                if (cmp != 0) return cmp;

                return tgtDepthA.CompareTo(tgtDepthB);
            });

            converter = candidates[0];
            return true;
        }

        private static void FindConverterCandidates(Type src, Type tgt, List<ITypeConverter> candidates)
        {
            var srcInterfaces = src.GetInterfaces();
            var tgtInterfaces = tgt.GetInterfaces();

            foreach (var srcIface in srcInterfaces)
            {
                foreach (var tgtIface in tgtInterfaces)
                {
                    if (srcIface.IsGenericType && tgtIface.IsGenericType)
                    {
                        var srcIfaceTemplate = srcIface.GetGenericTypeDefinition();
                        var tgtIfaceTemplate = tgtIface.GetGenericTypeDefinition();

                        if (_genericTemplates.TryGetValue((srcIfaceTemplate, tgtIfaceTemplate), out Type ifaceTemplate))
                        {
                            Type[] typeArgs = srcIface.GetGenericArguments();
                            Type closedConverter = ifaceTemplate.MakeGenericType(typeArgs);

                            if (Activator.CreateInstance(closedConverter) is ITypeConverter convInstance)
                            {
                                _cache.TryAdd((src, tgt), convInstance); // cache by actual src and tgt
                                candidates.Add(convInstance);
                                continue;
                            }
                        }
                    }
                    else
                    {
                        foreach (var kvp in _genericTemplates)
                        {
                            var srcTemplate = kvp.Key.Item1;
                            var tgtTemplate = kvp.Key.Item2;

                            if (!srcIface.IsGenericType && !tgtIface.IsGenericType &&
                                srcIface == srcTemplate && tgtIface == tgtTemplate)
                            {
                                var converterOpenGeneric = kvp.Value;

                                var genericParams = converterOpenGeneric.GetGenericArguments();

                                Type closedConverter;
                                if (genericParams.Length == 1)
                                {
                                    closedConverter = converterOpenGeneric.MakeGenericType(tgt);
                                }
                                else if (genericParams.Length == 2)
                                {
                                    closedConverter = converterOpenGeneric.MakeGenericType(src, tgt);
                                }
                                else
                                {
                                    closedConverter = converterOpenGeneric.MakeGenericType(src, tgt);
                                }

                                if (Activator.CreateInstance(closedConverter) is ITypeConverter convInstance)
                                {
                                    _cache.TryAdd((convInstance.SourceType, convInstance.TargetType), convInstance);
                                    candidates.Add(convInstance);
                                }
                            }
                        }
                    }
                }
            }

            if (src.IsGenericType && tgt.IsGenericType)
            {
                var srcTemplate = src.GetGenericTypeDefinition();
                var tgtTemplate = tgt.GetGenericTypeDefinition();

                if (_genericTemplates.TryGetValue((srcTemplate, tgtTemplate), out Type template))
                {
                    var tplParams = template.GetGenericArguments();
                    var srcArgs = src.GetGenericArguments();
                    var tgtArgs = tgt.GetGenericArguments();

                    Type closed;

                    if (tplParams.Length == 1)
                    {
                        closed = template.MakeGenericType(tgtArgs[0]);
                    }
                    else if (tplParams.Length == 2)
                    {
                        if (srcArgs.Length == 1 && tgtArgs.Length == 2)
                            closed = template.MakeGenericType(srcArgs[0], tgtArgs[1]);
                        else
                            closed = template.MakeGenericType(srcArgs[0], tgtArgs[0]);
                    }
                    else
                        return;

                    if (Activator.CreateInstance(closed) is ITypeConverter conv)
                    {
                        _cache.TryAdd((src, tgt), conv);
                        candidates.Add(conv);
                    }
                }
            }
        }

        private static int GetInheritanceDepth(Type type)
        {
            int depth = 0;
            Type? current = type;
            while (current != null)
            {
                depth++;
                current = current.BaseType;
            }
            return depth;
        }

        private static void DiscoverConverters()
        {
            IEnumerable<Type> types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => !t.IsAbstract &&
                            typeof(ITypeConverter).IsAssignableFrom(t));

            foreach (Type t in types)
            {
                Type? cur = t;
                while (cur is { } && cur != typeof(object))
                {
                    if (cur.IsGenericType &&
                        cur.GetGenericTypeDefinition() == typeof(TypeConverter<,>))
                        break;
                    cur = cur.BaseType;
                }
                if (cur is null) continue;

                var genericArgs = cur.GetGenericArguments();
                Type srcTemplate = GetTemplate(genericArgs[0]);
                Type tgtTemplate = GetTemplate(genericArgs[1]);

                if (t.IsGenericTypeDefinition)
                    _genericTemplates.TryAdd((srcTemplate, tgtTemplate), t);
                else if (Activator.CreateInstance(t) is ITypeConverter inst)
                    _cache.TryAdd((inst.SourceType, inst.TargetType), inst);

                static Type GetTemplate(Type x) =>
                    x.IsGenericType ? x.GetGenericTypeDefinition() : x;
            }
        }

        public static TTarget Convert<TTarget>(object source)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));

            Type src = source.GetType();
            Type tgt = typeof(TTarget);

            if (TryGetConverterAssignable(src, tgt, out ITypeConverter conv))
                return (TTarget)conv.Convert(source);

            if (src.IsGenericType && tgt.IsGenericType)
            {
                var key = (src.GetGenericTypeDefinition(), tgt.GetGenericTypeDefinition());
                if (_genericTemplates.TryGetValue(key, out Type template))
                {
                    Type closed = template.MakeGenericType(src.GetGenericArguments());
                    conv = (ITypeConverter)Activator.CreateInstance(closed)!;
                    _cache[(src, tgt)] = conv;
                    return (TTarget)conv.Convert(source);
                }
            }

            // NEW: try two‑step conversion
            if (TryFindPath(src, tgt, out var chain))
            {
                object obj = source;
                foreach (var step in chain)
                    obj = step.Convert(obj);

                return (TTarget)obj;
            }

            throw new InvalidOperationException(
                $"No converter registered from {src} to {tgt}");
        }
        public static object Convert(object source, Type targetType)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (targetType is null) throw new ArgumentNullException(nameof(targetType));

            Type src = source.GetType();
            Type tgt = targetType;

            // closed‑converter fast path
            if (_cache.TryGetValue((src, tgt), out ITypeConverter conv))
                return conv.Convert(source);

            // open‑generic fallback (same rules as before)
            if (src.IsGenericType && tgt.IsGenericType)
            {
                var tmplKey = (src.GetGenericTypeDefinition(), tgt.GetGenericTypeDefinition());
                if (_genericTemplates.TryGetValue(tmplKey, out Type template))
                {
                    conv = (ITypeConverter)Activator.CreateInstance(
                             template.MakeGenericType(src.GetGenericArguments()))!;

                    _cache[(src, tgt)] = conv;         // memoise closed instance
                    return conv.Convert(source);
                }
            }

            if (TryFindPath(src, tgt, out var chainObj))
            {
                object obj = source;
                foreach (var step in chainObj)
                    obj = step.Convert(obj);

                return obj;
            }

            throw new InvalidOperationException(
                $"No converter registered from {src} to {tgt}");
        }

        public static bool TryConvert(object source, Type targetType, out object result)
        {
            if (source is null || targetType is null)
            {
                result = null!;
                return false;
            }

            Type src = source.GetType();
            Type tgt = targetType;

            if (TryGetConverterAssignable(src, tgt, out ITypeConverter conv))
            {
                result = conv.Convert(source);
                return true;
            }

            if (src.IsGenericType && tgt.IsGenericType)
            {
                var tmplKey = (src.GetGenericTypeDefinition(), tgt.GetGenericTypeDefinition());

                if (_genericTemplates.TryGetValue(tmplKey, out Type template))
                {
                    conv = (ITypeConverter)Activator.CreateInstance(
                               template.MakeGenericType(src.GetGenericArguments()))!;

                    _cache[(src, tgt)] = conv;          // memoise closed instance
                    result = conv.Convert(source);
                    return true;
                }
            }

            if (TryFindPath(src, tgt, out var chainTry))
            {
                object tmp = source;
                foreach (var step in chainTry)
                    tmp = step.Convert(tmp);

                result = tmp;
                return true;
            }

            result = null!;
            return false;
        }
        public static bool TryConvert<TTarget>(object source, out TTarget result)
        {
            if (source is null)
            {
                result = default!;
                return false;
            }

            Type src = source.GetType();
            Type tgt = typeof(TTarget);

            // direct hit in the closed‑converter cache
            if (TryGetConverterAssignable(src, tgt, out ITypeConverter conv))
            {
                result = (TTarget)conv.Convert(source);
                return true;
            }

            // fall back to an open‑generic template, if one matches
            if (src.IsGenericType && tgt.IsGenericType)
            {
                var templKey = (src.GetGenericTypeDefinition(), tgt.GetGenericTypeDefinition());

                if (_genericTemplates.TryGetValue(templKey, out Type template))
                {
                    conv = (ITypeConverter)Activator.CreateInstance(
                             template.MakeGenericType(src.GetGenericArguments()))!;

                    _cache[(src, tgt)] = conv;          // memoise the closed instance
                    result = (TTarget)conv.Convert(source);
                    return true;
                }
            }

            result = default!;
            return false;
        }

        public static bool CanConvert(Type sourceType, Type targetType)
        {
            if (sourceType is null) throw new ArgumentNullException(nameof(sourceType));
            if (targetType is null) throw new ArgumentNullException(nameof(targetType));

            if (_cache.ContainsKey((sourceType, targetType)))
                return true;

            if (sourceType.IsGenericType && targetType.IsGenericType)
            {
                var key = (sourceType.GetGenericTypeDefinition(),
                           targetType.GetGenericTypeDefinition());
                if (_genericTemplates.ContainsKey(key))
                    return true;
            }

            return TryFindPath(sourceType, targetType, out _);
        }
        public static bool CanConvert<TSource, TTarget>() =>
            CanConvert(typeof(TSource), typeof(TTarget));
    }

    /// <summary>
    /// Generic base class, inherit from this and implement <see cref="Convert"/>.
    /// Instances auto-register at start-up, so no boilerplate registration code.
    /// </summary>
    public abstract class TypeConverter<TSource, TTarget> : ITypeConverter
    {
        /// <summary>
        /// the type of <typeparamref name="TSource"/>
        /// </summary>
        public Type SourceType => typeof(TSource);
        /// <summary>
        /// The type of <typeparamref name="TTarget"/>
        /// </summary>
        public Type TargetType => typeof(TTarget);

        /// <summary>
        /// When overriden in a derived class, converts <typeparamref name="TSource"/> to <typeparamref name="TTarget"/>
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public abstract TTarget Convert(TSource source);

        object ITypeConverter.Convert(object source) => Convert((TSource)source);
    }
}
