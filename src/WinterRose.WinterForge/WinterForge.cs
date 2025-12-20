using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using WinterRose.NetworkServer;
using WinterRose.WinterForgeSerializing.Compiling;
using WinterRose.WinterForgeSerializing.Formatting;
using WinterRose.WinterForgeSerializing.Instructions;
using WinterRose.WinterForgeSerializing.Logging;
using WinterRose.WinterForgeSerializing.Util;
using WinterRose.WinterForgeSerializing.Workers;

namespace WinterRose.WinterForgeSerializing
{
    /// <summary>
    /// The main delegation class for the WinterForge serialization system. <br></br>
    /// First use tends to take a whole lot longer due to caching and JIT.<br></br><br></br>
    /// Do you have feedback, or are you hitting a wall? Feel free to reach out on Discord: '<b>thesnowowl</b>'
    /// </summary>
    public static class WinterForge
    {
        /// <summary>
        /// The working directory WinterForge uses. accessd by "#workingDir()" <br />
        /// This property is whitelisted for the default WinterForge class access filter
        /// </summary>
        public static string WorkingDir { get; set; } = Directory.GetCurrentDirectory();
        /// <summary>
        /// The directory scripts will be imported from when using a #import statement
        /// </summary>
        public static string ImportDir { get; set; } = WorkingDir;

        /// <summary>
        /// When true, all default access filters will NOT be applied.<br /> Default is to false 
        /// </summary>
        public static bool NoDefaultAccessFilters { get; set; } = false;

        /// <summary>
        /// Whether or not the scripting part of winterforge is enabled <br />
        /// That being templates(functions), containers(classes), and loops <br />
        /// Accessing is not a part of this. <br /> <br/>
        ///
        /// Default: <see cref="ScriptingLevel.Conditions"/>
        /// </summary>
        public static ScriptingLevel AllowedScriptingLevel { get; set; } = ScriptingLevel.None;

        public enum ScriptingLevel
        {
            None,
            Conditions,
            All
        }
        
        /// <summary>
        /// The primitive types that are handled as-is by WinterForge
        /// </summary>
        public static List<Type> SupportedPrimitives { get; } =
           [
                typeof(bool),
                typeof(byte),
                typeof(sbyte),
                typeof(char),
                typeof(decimal),
                typeof(double),
                typeof(float),
                typeof(int),
                typeof(uint),
                typeof(long),
                typeof(ulong),
                typeof(short),
                typeof(ushort),
                typeof(string)
           ];

        /// <inheritdoc cref="WinterForgeGlobalAccessRestriction"/>
        public static WinterForgeGlobalAccessRestriction GlobalAccessRestriction { get; set; } = WinterForgeGlobalAccessRestriction.NoGlobalBlock;
        /// <summary>
        /// Setting this to false can increase the load times of certain larger datasets, however the scripting side of WinterForge can have issues with this is enabled
        /// </summary>
        public static bool AllowCustomCompilers { get; set; } = true;
        /// <summary>
        /// When true, all serialization streams will be compressed using GZip compression.
        /// It will also expect compressed streams when deserializing, but will also attempt to read it as uncompressed if that fails.
        /// </summary>
        public static bool CompressedStreams { get; set; }

        private static void EnsurePathExists(string path)
        {
            List<string> list = path.Split('/', '\\').ToList();
            if (list.Count > 1)
            {
                list.RemoveAt(list.Count - 1);
                string path2 = string.Join("/", list);
                if (!Directory.Exists(path2))
                {
                    Directory.CreateDirectory(path2);
                }
            }
        }

        /// <summary>
        /// Serializes the given object directly to opcodes for fastest deserialization
        /// </summary>
        /// <param name="o"></param>
        /// <param name="path"></param>
        public static void SerializeToFile(object o, string path, TargetFormat targetFormat = TargetFormat.Optimized, WinterForgeProgressTracker? progressTracker = null)
        {
            EnsurePathExists(path);

            using (Stream serialized = new MemoryStream())
            using (Stream opcodes = File.Open(path, FileMode.Create, FileAccess.ReadWrite))
            {
                ObjectSerializer serializer = new(progressTracker);
                DoSerialization(serializer, o, serialized, opcodes, targetFormat);
            }
        }
        /// <summary>
        /// Serializes the object into a string
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static string SerializeToString(object o, TargetFormat targetFormat = TargetFormat.Optimized, WinterForgeProgressTracker? progressTracker = null)
        {
            using MemoryStream serialized = new();
            using MemoryStream formatted = new();

            ObjectSerializer serializer = new(progressTracker);

            DoSerialization(serializer, o, serialized, formatted, targetFormat);
            byte[] bytes = formatted.ToArray();

            if (targetFormat is TargetFormat.HumanReadable or TargetFormat.FormattedHumanReadable)
            {
                StringBuilder sb = new StringBuilder();
                foreach (byte b in bytes)
                    sb.Append((char)b);
                return sb.ToString();
            }
            
            return Convert.ToBase64String(bytes);
        }
        /// <summary>
        /// Serializes the object into the stream
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="data"></param>
        public static void SerializeToStream(object obj, Stream data, TargetFormat targetFormat = TargetFormat.Optimized, WinterForgeProgressTracker? progressTracker = null)
        {
            using MemoryStream serialized = new MemoryStream();
            ObjectSerializer serializer = new(progressTracker);
            DoSerialization(serializer, obj, serialized, data, targetFormat);
        }
        /// <summary>
        /// Serializes the given static type to the file at the given <paramref name="path"/>
        /// </summary>
        /// <param name="type"></param>
        /// <param name="path"></param>
        /// <param name="targetFormat"></param>
        /// <param name="progressTracker"></param>
        public static void SerializeStaticToFile(Type type, string path, TargetFormat targetFormat = TargetFormat.Optimized, WinterForgeProgressTracker? progressTracker = null)
        {
            EnsurePathExists(path);

            using (Stream serialized = new MemoryStream())
            using (Stream opcodes = File.Open(path, FileMode.Create, FileAccess.ReadWrite))
            {
                ObjectSerializer serializer = new(progressTracker);
                DoStaticSerialization(serializer, type, serialized, opcodes, targetFormat);
            }
        }
        /// <summary>
        /// Serializes the given static type to a string
        /// </summary>
        /// <param name="type"></param>
        /// <param name="targetFormat"></param>
        /// <param name="progressTracker"></param>
        /// <returns></returns>
        public static string SerializeStaticToString(Type type, TargetFormat targetFormat = TargetFormat.Optimized, WinterForgeProgressTracker? progressTracker = null)
        {
            using MemoryStream serialized = new();
            using MemoryStream formatted = new();

            ObjectSerializer serializer = new(progressTracker);
            DoStaticSerialization(serializer, type, serialized, formatted, targetFormat);

            byte[] bytes = formatted.ToArray();
            return Encoding.UTF8.GetString(bytes);
        }
        /// <summary>
        /// Serializes the given static type to the given stream <paramref name="data"/>
        /// </summary>
        /// <param name="type"></param>
        /// <param name="data"></param>
        /// <param name="targetFormat"></param>
        /// <param name="progressTracker"></param>
        public static void SerializeStaticToStream(Type type, Stream data, TargetFormat targetFormat = TargetFormat.Optimized, WinterForgeProgressTracker? progressTracker = null)
        {
            using MemoryStream serialized = new();
            ObjectSerializer serializer = new(progressTracker);
            DoStaticSerialization(serializer, type, serialized, data, targetFormat);
        }

        /// <summary>
        /// Serializes to a file in the provided <paramref name="format"/>
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="path"></param>
        /// <param name="format"></param>
        /// <param name="progressTracker"></param>
        /// <returns></returns>
        public static WinterForgeSerializationTask SerializeToFileAsync(object obj, string path, TargetFormat format = TargetFormat.Optimized, WinterForgeProgressTracker? progressTracker = null) => RunSerializeAsync(() => SerializeToFile(obj, path, format, progressTracker));
        /// <summary>
        /// Serializes to a string in the provided <paramref name="format"/>
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="format"></param>
        /// <param name="progressTracker"></param>
        /// <returns></returns>
        public static WinterForgeSerializationTask SerializeToStringAsync(object obj, TargetFormat format = TargetFormat.Optimized, WinterForgeProgressTracker? progressTracker = null) => RunSerializeAsync(() => SerializeToString(obj, format, progressTracker));
        /// <summary>
        /// Serializes to a stream in the provided <paramref name="format"/>
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="stream"></param>
        /// <param name="format"></param>
        /// <param name="progressTracker"></param>
        /// <returns></returns>
        public static WinterForgeSerializationTask SerializeToStreamAsync(object obj, Stream stream, TargetFormat format = TargetFormat.Optimized, WinterForgeProgressTracker? progressTracker = null) => RunSerializeAsync(() => SerializeToStream(obj, stream, format, progressTracker));
        /// <summary>
        /// Serializes a static type to a file in the provided <paramref name="format"/>
        /// </summary>
        /// <param name="type"></param>
        /// <param name="path"></param>
        /// <param name="format"></param>
        /// <param name="progressTracker"></param>
        /// <returns></returns>
        public static WinterForgeSerializationTask SerializeStaticToFileAsync(Type type, string path, TargetFormat format = TargetFormat.Optimized, WinterForgeProgressTracker? progressTracker = null) => RunSerializeAsync(() => SerializeStaticToFile(type, path, format, progressTracker));
        /// <summary>
        /// Serializes a static type to a string in the provided <paramref name="format"/>
        /// </summary>
        /// <param name="type"></param>
        /// <param name="format"></param>
        /// <param name="progressTracker"></param>
        /// <returns></returns>
        public static WinterForgeSerializationTask SerializeStaticToStringAsync(Type type, TargetFormat format = TargetFormat.Optimized, WinterForgeProgressTracker? progressTracker = null) => RunSerializeAsync(() => SerializeStaticToString(type, format, progressTracker));
        /// <summary>
        /// Serializes a static type to a stream in the provided <paramref name="format"/>
        /// </summary>
        /// <param name="type"></param>
        /// <param name="stream"></param>
        /// <param name="format"></param>
        /// <param name="progressTracker"></param>
        /// <returns></returns>
        public static WinterForgeSerializationTask SerializeStaticToStreamAsync(Type type, Stream stream, TargetFormat format = TargetFormat.Optimized, WinterForgeProgressTracker? progressTracker = null) => RunSerializeAsync(() => SerializeStaticToStream(type, stream, format, progressTracker));

        private static void FinishSerialization(Stream serialized, Stream opcodes, TargetFormat target)
        {
            Stream outputStream = opcodes;

            if (CompressedStreams)
                outputStream = new GZipStream(opcodes, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true);

            if (target is TargetFormat.HumanReadable)
            {
                serialized.CopyTo(outputStream);
                outputStream.Flush();
            }
            else if (target is TargetFormat.FormattedHumanReadable)
            {
                new HumanReadableIndenter().Process(serialized, outputStream);
            }
            else
            {
                if (target is TargetFormat.IntermediateRepresentation)
                {
                    new HumanReadableParser().Parse(serialized, outputStream);
                }
                else if (target is TargetFormat.ReadableIntermediateRepresentation)
                {
                    using MemoryStream mem = new();
                    new HumanReadableParser().Parse(serialized, mem);
                    mem.Position = 0;
                    new OpcodeToReadableOpcodeParser().Parse(mem, outputStream);
                }
                else
                {
                    using OpcodeToByteCompiler compiler = new(opcodes, AllowCustomCompilers);
                    new HumanReadableParser().Parse(serialized, compiler);
                }
            }

            outputStream.Flush();

            if (CompressedStreams)
                outputStream.Dispose(); // flushes and closes GZip wrapper without closing underlying opcodes stream
        }


        /// <summary>
        /// Deserializes from the given stream in which opcodes should exist
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        public static object? DeserializeFromStream(Stream stream, WinterForgeProgressTracker? progressTracker = null)
        {
            using CacheReader cacheStream = new(stream, new MemoryStream());

            try
            {
                using GZipStream compressStream = new GZipStream(cacheStream, CompressionMode.Decompress, leaveOpen: true);
                using TempFileStream temp = new(compressStream);
                return CommitDeserialize(progressTracker, temp);
            }
            catch (InvalidDataException e)
            {
                var backup = cacheStream.CreateFallbackReader();
                return CommitDeserialize(progressTracker, backup);
            }

            static object? CommitDeserialize(WinterForgeProgressTracker? progressTracker, Stream compressStream)
            {
                InstructionStream instr = ByteToOpcodeDecompiler.Parse(compressStream);
                object? result = null;
                DoDeserialization(out result, typeof(Nothing), instr, progressTracker);
                return result;
            }
        }
        /// <summary>
        /// Deserializes from the given stream in which opcodes should exist
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="stream"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        public static T? DeserializeFromStream<T>(Stream stream, WinterForgeProgressTracker? progressTracker = null)
        {
            return (T?)DeserializeFromStream(stream, progressTracker);
        }
        /// <summary>
        /// Deserializes from the given string of opcodes
        /// </summary>
        /// <param name="opcodes"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        public static object? DeserializeFromString(string opcodes, WinterForgeProgressTracker? progressTracker = null)
        {
            using MemoryStream ops = new MemoryStream(Convert.FromBase64String(opcodes));
            return DeserializeFromStream(ops, progressTracker);
        }
        /// <summary>
        /// Deserializes from the given string of opcodes
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="opcodes"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        public static T? DeserializeFromString<T>(string opcodes, WinterForgeProgressTracker? progressTracker = null)
        {
            return (T?)DeserializeFromString(opcodes, progressTracker);
        }
        /// Deserializes from the given human readable string
        /// </summary>
        /// <param name="humanReadable"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        public static object? DeserializeFromHumanReadableString(string humanReadable, WinterForgeProgressTracker? progressTracker = null)
        {
            using var opcodes = new MemoryStream();
            using var serialized = new MemoryStream();

            byte[] humanBytes = Encoding.UTF8.GetBytes(humanReadable);
            serialized.Write(humanBytes, 0, humanBytes.Length);
            serialized.Seek(0, SeekOrigin.Begin);

            new HumanReadableParser().Parse(serialized, opcodes);
            opcodes.Seek(0, SeekOrigin.Begin);

            var instructions = ByteToOpcodeDecompiler.Parse(serialized);
            DoDeserialization(out object? res, typeof(Nothing), instructions, progressTracker);
            return res;
        }
        /// <summary>
        /// 
        /// Deserializes from the given human readable string
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="humanReadable"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        public static T? DeserializeFromHumanReadableString<T>(string humanReadable, WinterForgeProgressTracker? progressTracker = null)
        {
            return (T?)DeserializeFromHumanReadableString(humanReadable, progressTracker);
        }
        /// <summary>
        /// Deserializes from the given human readable stream
        /// </summary>
        /// <param name="humanReadable"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        public static object? DeserializeFromHumanReadableStream(Stream humanReadable, WinterForgeProgressTracker? progressTracker = null)
        {
            using var opcodes = new MemoryStream();

            FinishSerialization(humanReadable, opcodes, TargetFormat.Optimized);
            opcodes.Position = 0;

            return DeserializeFromStream(opcodes, progressTracker);
        }
        /// <summary>
        /// Deserializes from the given human readable stream
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="humanReadable"></param>
        /// <returns></returns>
        public static T? DeserializeFromHumanReadableStream<T>(Stream humanReadable, WinterForgeProgressTracker? progressTracker = null)
        {
            return (T?)DeserializeFromHumanReadableStream(humanReadable, progressTracker);
        }
        /// <summary>
        /// Deserialies from the given file that has the human readable format
        /// </summary>
        /// <param name="path"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        public static object? DeserializeFromHumanReadableFile(string path, WinterForgeProgressTracker? progressTracker = null)
        {
            using var mem = new MemoryStream();
            using var humanReadable = File.OpenRead(path);

            new HumanReadableParser().Parse(humanReadable, mem);
            mem.Seek(0, SeekOrigin.Begin);
            using var opcodes = new MemoryStream();
            using var compiled = new OpcodeToByteCompiler(opcodes, AllowCustomCompilers);
            compiled.Position = 0;
            var instructions = ByteToOpcodeDecompiler.Parse(compiled);
            DoDeserialization(out object? res, typeof(Nothing), instructions, progressTracker);
            return res;
        }
        /// <summary>
        /// Deserialies from the given file that has the human readable format
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="path"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        public static T? DeserializeFromHumanReadableFile<T>(string path, WinterForgeProgressTracker? progressTracker = null)
        {
            return (T?)DeserializeFromHumanReadableFile(path, progressTracker);
        }
        /// <summary>
        /// Deserializes from the file that has the opcodes
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static object? DeserializeFromFile(string path, WinterForgeProgressTracker? progressTracker = null)
        {
            using Stream opcodes = File.OpenRead(path);
            return DeserializeFromStream(opcodes, progressTracker);
        }

        /// <summary>
        /// Deserializes from the file that has the opcodes
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T? DeserializeFromFile<T>(string path, WinterForgeProgressTracker? progressTracker = null)
        {
            object? res = DeserializeFromFile(path, progressTracker);
            if (res is not T t)
            {
                if (res is Nothing)
                    throw new WinterForgeExecutionException($"Requested data deserialzied into 'Nothing' meaning no result was returned. yet a result of type {ObjectSerializer.ParseTypeName(typeof(T))} is expected");
                throw new InvalidCastException($"Deserialized object is of type {ObjectSerializer.ParseTypeName(res?.GetType(), true)}, cannot cast to {ObjectSerializer.ParseTypeName(typeof(T))}");
            }
            return t;
        }

        /// <summary>
        /// Deserializes from the given opcode stream
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="progressTracker"></param>
        /// <returns></returns>
        public static WinterForgeDeserializationTask<object> DeserializeFromStreamAsync(Stream stream, WinterForgeProgressTracker? progressTracker = null) =>
            RunAsync(() => DeserializeFromStream(stream, progressTracker));
        /// <summary>
        /// Deserializes from the given opcode stream
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="stream"></param>
        /// <param name="progressTracker"></param>
        /// <returns></returns>
        public static WinterForgeDeserializationTask<T?> DeserializeFromStreamAsync<T>(Stream stream, WinterForgeProgressTracker? progressTracker = null) =>
            RunAsync(() => DeserializeFromStream<T>(stream, progressTracker));
        /// <summary>
        /// Deserializes from the given opcode string
        /// </summary>
        /// <param name="opcodes"></param>
        /// <param name="encoding"></param>
        /// <param name="progressTracker"></param>
        /// <returns></returns>
        public static WinterForgeDeserializationTask<object> DeserializeFromStringAsync(string opcodes, WinterForgeProgressTracker? progressTracker = null) =>
            RunAsync(() => DeserializeFromString(opcodes, progressTracker));
        /// <summary>
        /// Deserializes from the given opcode string
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="opcodes"></param>
        /// <param name="encoding"></param>
        /// <param name="progressTracker"></param>
        /// <returns></returns>
        public static WinterForgeDeserializationTask<T?> DeserializeFromStringAsync<T>(string opcodes, WinterForgeProgressTracker? progressTracker = null) =>
            RunAsync(() => DeserializeFromString<T>(opcodes, progressTracker));
        /// <summary>
        /// Deserializes from the given opcode file
        /// </summary>
        /// <param name="path"></param>
        /// <param name="progressTracker"></param>
        /// <returns></returns>
        public static WinterForgeDeserializationTask<object> DeserializeFromFileAsync(string path, WinterForgeProgressTracker? progressTracker = null) =>
            RunAsync(() => DeserializeFromFile(path, progressTracker));
        /// <summary>
        /// Deserializes from the given opcode file
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="path"></param>
        /// <param name="progressTracker"></param>
        /// <returns></returns>
        public static WinterForgeDeserializationTask<T?> DeserializeFromFileAsync<T>(string path, WinterForgeProgressTracker? progressTracker = null) =>
            RunAsync(() => DeserializeFromFile<T>(path, progressTracker));

        /// <summary>
        /// Deserializes from the given human readable string
        /// </summary>
        /// <param name="humanReadable"></param>
        /// <param name="progressTracker"></param>
        /// <returns></returns>
        public static WinterForgeDeserializationTask<object> DeserializeFromHumanReadableStringAsync(string humanReadable, WinterForgeProgressTracker? progressTracker = null) =>
            RunAsync(() => DeserializeFromHumanReadableString(humanReadable, progressTracker));
        /// <summary>
        /// Deserializes from the given human readable stirng
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="humanReadable"></param>
        /// <param name="progressTracker"></param>
        /// <returns></returns>
        public static WinterForgeDeserializationTask<T?> DeserializeFromHumanReadableStringAsync<T>(string humanReadable, WinterForgeProgressTracker? progressTracker = null) =>
            RunAsync(() => DeserializeFromHumanReadableString<T>(humanReadable, progressTracker));
        /// <summary>
        /// Deserializes from the given human readable stream
        /// </summary>
        /// <param name="humanReadable"></param>
        /// <param name="progressTracker"></param>
        /// <returns></returns>
        public static WinterForgeDeserializationTask<object> DeserializeFromHumanReadableStreamAsync(Stream humanReadable, WinterForgeProgressTracker? progressTracker = null) =>
            RunAsync(() => DeserializeFromHumanReadableStream(humanReadable, progressTracker));
        /// <summary>
        /// Deserializes from the given human readable stream
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="humanReadable"></param>
        /// <param name="progressTracker"></param>
        /// <returns></returns>
        public static WinterForgeDeserializationTask<T?> DeserializeFromHumanReadableStreamAsync<T>(Stream humanReadable, WinterForgeProgressTracker? progressTracker = null) =>
            RunAsync(() => DeserializeFromHumanReadableStream<T>(humanReadable, progressTracker));
        /// <summary>
        /// Deserializes from the given human readable file
        /// </summary>
        /// <param name="path"></param>
        /// <param name="progressTracker"></param>
        /// <returns></returns>
        public static WinterForgeDeserializationTask<object> DeserializeFromHumanReadableFileAsync(string path, WinterForgeProgressTracker? progressTracker = null) =>
            RunAsync(() => DeserializeFromHumanReadableFile(path, progressTracker));
        /// <summary>
        /// Deserializes from the given human readable file
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="path"></param>
        /// <param name="progressTracker"></param>
        /// <returns></returns>
        public static WinterForgeDeserializationTask<T?> DeserializeFromHumanReadableFileAsync<T>(string path, WinterForgeProgressTracker? progressTracker = null) =>
            RunAsync(() => DeserializeFromHumanReadableFile<T>(path, progressTracker));

        private static void DoDeserialization(out object? result, Type targetType, InstructionStream instructions, WinterForgeProgressTracker? progressTracker = null)
        {
            using var executor = new WinterForgeVM();
            if (progressTracker is not null)
                executor.progressTracker = progressTracker;
            object? res = executor.Execute(instructions, true);

            if (res is List<object> list)
            {
                if (targetType.IsArray)
                {
                    var array = Array.CreateInstance(targetType.GetElementType()!, list.Count);

                    for (int i = 0; i < list.Count; i++)
                        array.SetValue(list[i], i);

                    result = array;
                }

                if (targetType.Name.Contains("List`1"))
                {
                    var targetList = CreateList(targetType.GetGenericArguments()[0]);

                    for (int i = 0; i < list.Count; i++)
                        targetList.Add(list[i]);

                    result = targetList;
                }

                throw new Exception("invalid deserialization!");
            }

            result = res;
        }

        /// <summary>
        /// Converts the human readable format into opcodes
        /// </summary>
        /// <param name="humanreadable"></param>
        /// <param name="opcodeDestination"></param>
        public static void ConvertFromStreamToStream(Stream humanreadable, Stream opcodeDestination, TargetFormat target)
        {
            FinishSerialization(humanreadable, opcodeDestination, target);
        }
        /// <summary>
        /// Converts a human-readable file to an opcode file.
        /// </summary>
        public static void ConvertFromFileToFile(string inputPath, string outputPath, TargetFormat target = TargetFormat.Optimized)
        {
            EnsurePathExists(outputPath);
            using var input = File.OpenRead(inputPath);
            using var output = File.Create(outputPath);
            
            ConvertFromStreamToStream(input, output, target);
        }
        /// <summary>
        /// Converts a human-readable string to an opcode string.
        /// </summary>
        public static string ConvertFromStringToString(string input, TargetFormat target = TargetFormat.Optimized)
        {
            using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(input));
            using var outputStream = new MemoryStream();
            ConvertFromStreamToStream(inputStream, outputStream, target);
            outputStream.Position = 0;
            return Convert.ToBase64String(outputStream.ToArray());
        }
        /// <summary>
        /// Converts a human-readable file to an opcode string.
        /// </summary>
        public static string ConvertFromFileToString(string inputPath, TargetFormat target = TargetFormat.Optimized)
        {
            using var inputStream = File.OpenRead(inputPath);
            using var outputStream = new MemoryStream();
            ConvertFromStreamToStream(inputStream, outputStream, target);
            outputStream.Position = 0;
            return Convert.ToBase64String(outputStream.ToArray());
        }
        /// <summary>
        /// Converts a human-readable string to an opcode file.
        /// </summary>
        public static void ConvertFromStringToFile(string input, string outputPath, TargetFormat target = TargetFormat.Optimized)
        {
            EnsurePathExists(outputPath);
            using var inputStream = new MemoryStream(Convert.FromBase64String(input));
            using var outputStream = File.Create(outputPath);
            ConvertFromStreamToStream(inputStream, outputStream, target);
        }
        /// <summary>
        /// Converts a human-readable file to an opcode stream.
        /// </summary>
        public static Stream ConvertFromFileToStream(string inputPath, TargetFormat target = TargetFormat.Optimized)
        {
            using var inputStream = File.OpenRead(inputPath);
            var outputStream = new MemoryStream();
            ConvertFromStreamToStream(inputStream, outputStream, target);
            outputStream.Position = 0;
            return outputStream;
        }
        /// <summary>
        /// Converts a human-readable string to an opcode stream.
        /// </summary>
        public static Stream ConvertFromStringToStream(string input, TargetFormat target = TargetFormat.Optimized)
        {
            using var inputStream = new MemoryStream(Convert.FromBase64String(input));
            var outputStream = new MemoryStream();
            ConvertFromStreamToStream(inputStream, outputStream, target);
            outputStream.Position = 0;
            return outputStream;
        }

        private static void DoStaticSerialization(ObjectSerializer serializer, Type type, Stream serialized, Stream opcodes, TargetFormat target)
        {
            serializer.SerializeAsStatic(type, serialized);
            serialized.Seek(0, SeekOrigin.Begin);
            FinishSerialization(serialized, opcodes, target);
        }
        private static void DoSerialization(ObjectSerializer serializer, object o, Stream serialized, Stream opcodes, TargetFormat target)
        {
            serializer.Serialize(o, serialized);
            serialized.Seek(0, SeekOrigin.Begin);
            FinishSerialization(serialized, opcodes, target);
        }

        internal static IList CreateList(Type t)
        {
            var list = typeof(List<>);
            var constructedListType = list.MakeGenericType(t);

            return (IList)Activator.CreateInstance(constructedListType);
        }

        public static WinterForgeStreamInfo InspectStream(Stream stream)
        {
            using CacheReader cacheStream = new(stream, new MemoryStream());

            try
            {
                long compressedBytes = stream.CanSeek ? stream.Length : 0;

                using GZipStream compressStream = new(cacheStream, CompressionMode.Decompress, leaveOpen: true);
                using TempFileStream temp = new(compressStream);

                return InspectInstructions(temp, compressedBytes);
            }
            catch (InvalidDataException)
            {
                var backup = cacheStream.CreateFallbackReader();
                return InspectInstructions(backup, 0);
            }

            static WinterForgeStreamInfo InspectInstructions(Stream source, long compressedBytes)
            {
                long rawBytes = source.CanSeek ? source.Length : 0;

                InstructionStream instr = ByteToOpcodeDecompiler.Parse(source);

                Dictionary<Instructions.OpCode, int> histogram = new();
                int total = 0;

                for (int i = 0; ; i++)
                {
                    Instruction instruction;
                    try
                    {
                        instruction = instr[i];
                    }
                    catch (IndexOutOfRangeException)
                    {
                        break;
                    }

                    total++;

                    if (histogram.TryGetValue(instruction.Opcode, out int count))
                        histogram[instruction.Opcode] = count + 1;
                    else
                        histogram[instruction.Opcode] = 1;
                }

                return new WinterForgeStreamInfo(
                    total,
                    rawBytes,
                    compressedBytes,
                    histogram
                );
            }
        }

        // ── helper runners ────────────────────────────────────────────────────────
        private static WinterForgeDeserializationTask<object> RunAsync(Func<object?> work)
        {
            var task = new WinterForgeDeserializationTask<object>();
            Task.Run(() =>
            {
                try { task.Result = work(); }
                catch (Exception exception) { task.Exception = exception; }
            });
            return task;
        }

        private static WinterForgeSerializationTask RunSerializeAsync(Action work)
        {
            var task = new WinterForgeSerializationTask();
            Task.Run(() =>
            {
                try { work(); }
                catch (Exception exception) { task.Exception = exception; }
            });
            return task;
        }

        private static WinterForgeDeserializationTask<T?> RunAsync<T>(Func<T?> work)
        {
            var task = new WinterForgeDeserializationTask<T?>();
            Task.Run(() =>
            {
                try { task.Result = work(); }
                catch (Exception exception) { task.Exception = exception; }
            });
            return task;
        }

        internal static IDictionary CreateDictionary(Type keyType, Type valueType)
        {
            var dict = typeof(Dictionary<,>);
            var constructedDictType = dict.MakeGenericType(keyType, valueType);

            return (IDictionary)Activator.CreateInstance(constructedDictType)!;
        }

        internal static object DeserializeFromInstructions(InstructionStream instructions)
        {
            DoDeserialization(out object? r, typeof(object), instructions, null);
            return r;
        }
    }
}
