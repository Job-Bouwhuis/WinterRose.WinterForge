using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using System.Xml.Serialization;
using WinterRose.WinterForgeSerializing;
using WinterRose.WinterForgeSerializing.Formatting;
using WinterRose.WinterForgeSerializing.Workers;

namespace WinterRose.WinterForgeSerialization
{
    /// <summary>
    /// Used to limit the types and members of said type can be accessed through WinterForge
    /// </summary>
    public static class AccessFilterCache
    {
        /// <summary>
        /// all the filters
        /// </summary>
        [IncludeWithSerialization]
        public static ConcurrentDictionary<string, AccessFilter> Filters { get; private set; } = [];

        static AccessFilterCache()
        {
            GetFilter(typeof(File), AccessFilterKind.Whitelist);
            GetFilter(typeof(FileInfo), AccessFilterKind.Whitelist);
            GetFilter(typeof(Directory), AccessFilterKind.Whitelist);
            GetFilter(typeof(DirectoryInfo), AccessFilterKind.Whitelist);
            GetFilter(typeof(Path), AccessFilterKind.Whitelist);
            GetFilter(typeof(DriveInfo), AccessFilterKind.Whitelist);

            GetFilter(typeof(Process), AccessFilterKind.Whitelist);
            GetFilter(typeof(Environment), AccessFilterKind.Whitelist);
            GetFilter(typeof(AppDomain), AccessFilterKind.Whitelist);
            GetFilter(typeof(Console), AccessFilterKind.Whitelist);

            GetFilter(typeof(Type), AccessFilterKind.Whitelist);
            GetFilter(typeof(MemberInfo), AccessFilterKind.Whitelist);
            GetFilter(typeof(MethodInfo), AccessFilterKind.Whitelist);
            GetFilter(typeof(FieldInfo), AccessFilterKind.Whitelist);
            GetFilter(typeof(PropertyInfo), AccessFilterKind.Whitelist);
            GetFilter(typeof(Assembly), AccessFilterKind.Whitelist);
            GetFilter(typeof(Activator), AccessFilterKind.Whitelist);
            GetFilter(typeof(RuntimeTypeHandle), AccessFilterKind.Whitelist);

            GetFilter(typeof(Marshal), AccessFilterKind.Whitelist);
            GetFilter(typeof(GCHandle), AccessFilterKind.Whitelist);
            GetFilter(typeof(UnmanagedFunctionPointerAttribute), AccessFilterKind.Whitelist);

            GetFilter(typeof(WindowsIdentity), AccessFilterKind.Whitelist);
            GetFilter(typeof(WindowsPrincipal), AccessFilterKind.Whitelist);

            GetFilter(typeof(WebClient), AccessFilterKind.Whitelist);
            GetFilter(typeof(HttpClient), AccessFilterKind.Whitelist);
            GetFilter(typeof(WebRequest), AccessFilterKind.Whitelist);
            GetFilter(typeof(HttpWebRequest), AccessFilterKind.Whitelist);
            GetFilter(typeof(HttpWebResponse), AccessFilterKind.Whitelist);
            GetFilter(typeof(Socket), AccessFilterKind.Whitelist);
            GetFilter(typeof(TcpClient), AccessFilterKind.Whitelist);
            GetFilter(typeof(TcpListener), AccessFilterKind.Whitelist);

            GetFilter(typeof(DataContractSerializer), AccessFilterKind.Whitelist);
            GetFilter(typeof(XmlSerializer), AccessFilterKind.Whitelist);
            GetFilter(typeof(WinterForge), AccessFilterKind.Whitelist);
            GetFilter(typeof(ObjectSerializer), AccessFilterKind.Whitelist);
            GetFilter(typeof(HumanReadableParser), AccessFilterKind.Whitelist);
            GetFilter(typeof(HumanReadableIndenter), AccessFilterKind.Whitelist);
            GetFilter(typeof(InstructionExecutor), AccessFilterKind.Whitelist);
            GetFilter(typeof(InstructionParser), AccessFilterKind.Whitelist);

            GetFilter(typeof(Thread), AccessFilterKind.Whitelist);
            GetFilter(typeof(ThreadPool), AccessFilterKind.Whitelist);
            GetFilter(typeof(Task), AccessFilterKind.Whitelist);
            GetFilter(typeof(Timer), AccessFilterKind.Whitelist);

            GetFilter(typeof(Debug), AccessFilterKind.Whitelist);
            GetFilter(typeof(Trace), AccessFilterKind.Whitelist);
        }

        /// <summary>
        /// Gets the filter for the given <paramref name="type"/>. if it doesnt exist yet, a whitelist or blacklist is created depending on the global state
        /// </summary>
        /// <returns></returns>
        /// <exception cref="System.Diagnostics.UnreachableException"></exception>
        public static AccessFilter GetFilter(Type type, AccessFilterKind FilterKindOnNewFilter)
        {
            string? fullName = (type.IsGenericType ?
                type.GetGenericTypeDefinition().FullName :
                type.FullName) ?? throw new NullReferenceException($"Type {type} gave 'null' on 'type.FullName'");

            return Filters.GetOrAdd(fullName, fn => FilterKindOnNewFilter switch
            {
                AccessFilterKind.Whitelist => AccessFilter.CreateWhitelist(type, fn, null),
                AccessFilterKind.Blacklist => AccessFilter.CreateBlacklist(type, fn, null),
                _ => throw new System.Diagnostics.UnreachableException()
            });
        }

        /// <summary>
        /// Builds the cache with the given type filters
        /// </summary>
        public static void Cache(AccessFilterKind FilterKindOnNewFilter, params ReadOnlySpan<Type> types)
        {
            foreach (var t in types)
                _ = GetFilter(t, FilterKindOnNewFilter);
        }

        /// <summary>
        /// Builds the cache with the given type filters and applies the <paramref name="configurator"/> on each
        /// </summary>
        public static void Prefill(Action<AccessFilter> configurator, AccessFilterKind FilterKindOnNewFilter, params ReadOnlySpan<Type> types)
        {
            foreach (var t in types)
                configurator(GetFilter(t, FilterKindOnNewFilter));
        }

        internal static void Validate(Type type, AccessFilterKind FilterKindOnNewFilter, string v)
        {
            if (!GetFilter(type, FilterKindOnNewFilter).IsAllowed(v))
            {
                string? fullName = type.IsGenericType ?
                type.GetGenericTypeDefinition().FullName :
                type.FullName;

                throw new WinterForgeAccessIllegalException($"Accessing Member {fullName}.{v} is not allowed");
            }
        }
    }

    internal class WinterForgeAccessIllegalException(string msg) : Exception(msg);

    [Serializable]
    internal class WinterForgeAccessFilterException : Exception
    {
        public WinterForgeAccessFilterException()
        {
        }

        public WinterForgeAccessFilterException(string? message) : base(message)
        {
        }

        public WinterForgeAccessFilterException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Defines granular allow/deny rules for member access on a specific runtime type.
    /// </summary>
    public class AccessFilter
    {
        /// <summary>
        /// Fully‑qualified name (e.g. <c>"System.Collections.Generic.List`1"</c>) of the target type.
        /// </summary>
        [IncludeWithSerialization]
        public string TypeName { get; private set; }

        /// <summary>
        /// Runtime <see cref="Type"/> resolved from <see cref="TypeName"/>
        /// </summary>
        [IncludeWithSerialization]
        public Type Type { get; private set; }

        /// <summary>
        /// Explicit member names governed by this filter.  <br></br>
        /// Whitelist mode > members allowed  <br></br>
        /// Blacklist mode > members blocked
        /// </summary>
        [IncludeWithSerialization]
        public HashSet<string> GovernedMembers { get; private set; } = [];

        /// <summary>
        /// These members are explicitly exemt from the filter.
        /// </summary>
        [IncludeWithSerialization]
        public HashSet<string> ExceptThese { get; private set; } = [];

        /// <summary>
        /// The kind of filter this is
        /// </summary>
        [IncludeWithSerialization]
        public AccessFilterKind FilterKind { get; set; }

        /// <summary>
        /// <see langword="true"/> if this instance represents a whitelist, otherwise it acts as a blacklist.
        /// </summary> 
        public bool IsWhitelist => FilterKind is AccessFilterKind.Whitelist;

        /// <summary>
        /// <see langword="true"/> if this instance represents a blacklist, otherwise it acts as a whitelist.
        /// </summary>
        public bool IsBlacklist => FilterKind is AccessFilterKind.Blacklist;

        private AccessFilter()
        {

        }

        internal static AccessFilter CreateWhitelist(Type type, string typeName, Action<AccessFilter>? configurator = null)
        {
            AccessFilter filter = new();
            filter.TypeName = typeName;
            filter.Type = type;
            filter.FilterKind = AccessFilterKind.Whitelist;
            configurator?.Invoke(filter);
            return filter;
        }

        internal static AccessFilter CreateBlacklist(Type type, string typeName, Action<AccessFilter>? configurator = null)
        {
            AccessFilter filter = new();
            filter.TypeName = typeName;
            filter.Type = type;
            filter.FilterKind = AccessFilterKind.Blacklist;
            configurator?.Invoke(filter);
            return filter;
        }

        /// <summary>
        /// Whether the filter allows or disallows the member with the given name. does <b>not</b> govern for member names that dont exist on the type
        /// </summary>
        /// <param name="memberName"></param>
        /// <returns></returns>
        public bool IsAllowed(string memberName)
        {
            if (memberName.StartsWith("get_", StringComparison.Ordinal) ||
                memberName.StartsWith("set_", StringComparison.Ordinal))
                return false; // default to block property getters and setters for they should be accessed through the property, not the direct methods

            if (ExceptThese.Contains(memberName))
                return !IsWhitelist;

            bool listed = GovernedMembers.Contains(memberName);
            return IsWhitelist ? listed : !listed;
        }

        /// <summary>
        /// Adds all the provided members to this filter
        /// </summary>
        /// <param name="members"></param>
        public void Govern(params ReadOnlySpan<string> members)
        {
            foreach (var m in members)
                GovernedMembers.Add(m);
        }

        /// <summary>
        /// Adds all the provided members to <see cref="ExceptThese"/>
        /// </summary>
        /// <param name="members"></param>
        public void Except(params ReadOnlySpan<string> members)
        {
            foreach (var m in members)
                ExceptThese.Add(m);
        }
    }

    /// <summary>
    /// Global default policy that WinterForge applies when evaluating <see cref="AccessFilter"/>s.
    /// </summary>
    public enum AccessFilterKind
    {
        /// <summary>
        /// Members are permitted unless they are within the filter
        /// </summary>
        Blacklist,
        /// <summary>
        /// Members are permitted only when they are in the filter
        /// </summary>
        Whitelist
    }
}
