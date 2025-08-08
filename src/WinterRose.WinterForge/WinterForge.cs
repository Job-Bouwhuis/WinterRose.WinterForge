using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using WinterRose.WinterForgeSerializing.Instructions;
using WinterRose.WinterForgeSerializing.Util;
using WinterRose.WinterForgeSerializing.Compiling;
using WinterRose.WinterForgeSerializing.Formatting;
using WinterRose.WinterForgeSerializing.Logging;
using WinterRose.WinterForgeSerializing.Workers;

namespace WinterRose.WinterForgeSerializing
{
    /// <summary>
    /// The main delegation class for the WinterForge serialization system. <br></br>
    /// First use tends to take a whole lot longer due to caching.<br></br><br></br>
    /// Do you have feedback, or are you hitting a wall? Feel free to reach out on Discord: '<b>thesnowowl</b>'
    /// </summary>
    public static class WinterForge
    {
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
            return Encoding.UTF8.GetString(bytes);
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


        /// <summary>
        /// Deserializes from the given stream in which opcodes should exist
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        public static object? DeserializeFromStream(Stream stream, WinterForgeProgressTracker? progressTracker = null)
        {
            var instr = ByteToOpcodeParser.Parse(stream);
            object? result = null;
            DoDeserialization(out result, typeof(Nothing), instr, progressTracker);
            return result;
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
            using MemoryStream ops = new MemoryStream(Encoding.UTF8.GetBytes(opcodes));
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

            var instructions = ByteToOpcodeParser.Parse(serialized);
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

            new HumanReadableParser().Parse(humanReadable, opcodes);
            opcodes.Seek(0, SeekOrigin.Begin);

            var instructions = ByteToOpcodeParser.Parse(opcodes);
            DoDeserialization(out object? res, typeof(Nothing), instructions, progressTracker);
            return res;
        }
        /// <summary>
        /// Deserializes from the given human readable stream
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="humanReadable"></param>
        /// <param name="progress"></param>
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
            new OpcodeToByteCompiler().Compile(mem, opcodes);
            opcodes.Position = 0;
            var instructions = ByteToOpcodeParser.Parse(opcodes);
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
            var instructions = ByteToOpcodeParser.Parse(opcodes);
            DoDeserialization(out object? res, typeof(Nothing), instructions, progressTracker);
            return res;
        }

        /// <summary>
        /// Deserializes from the file that has the opcodes
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="path"></param>
        /// <returns></returns>
        public static T? DeserializeFromFile<T>(string path, WinterForgeProgressTracker? progressTracker = null)
        {
            return (T?)DeserializeFromFile(path, progressTracker);
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

        /// <summary>
        /// Converts the human readable format into opcodes
        /// </summary>
        /// <param name="humanreadable"></param>
        /// <param name="opcodeDestination"></param>
        public static void ConvertFromStreamToStream(Stream humanreadable, Stream opcodeDestination)
        {
            using MemoryStream mem = new();
            new HumanReadableParser().Parse(humanreadable, mem);
            mem.Position = 0;
            new OpcodeToByteCompiler().Compile(mem, opcodeDestination);

            long memlength = mem.Length;
            long reslength = opcodeDestination.Length;
        }
        /// <summary>
        /// Converts a human-readable file to an opcode file.
        /// </summary>
        public static void ConvertFromFileToFile(string inputPath, string outputPath)
        {
            EnsurePathExists(outputPath);
            using var input = File.OpenRead(inputPath);
            using var output = File.Create(outputPath);

            ConvertFromStreamToStream(input, output);
        }
        /// <summary>
        /// Converts a human-readable string to an opcode string.
        /// </summary>
        public static string ConvertFromStringToString(string input)
        {
            using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(input));
            using var outputStream = new MemoryStream();
            ConvertFromStreamToStream(inputStream, outputStream);
            outputStream.Position = 0;
            using var reader = new StreamReader(outputStream);
            return reader.ReadToEnd();
        }
        /// <summary>
        /// Converts a human-readable file to an opcode string.
        /// </summary>
        public static string ConvertFromFileToString(string inputPath)
        {
            using var inputStream = File.OpenRead(inputPath);
            using var outputStream = new MemoryStream();
            ConvertFromStreamToStream(inputStream, outputStream);
            outputStream.Position = 0;
            using var reader = new StreamReader(outputStream);
            return reader.ReadToEnd();
        }
        /// <summary>
        /// Converts a human-readable string to an opcode file.
        /// </summary>
        public static void ConvertFromStringToFile(string input, string outputPath)
        {
            EnsurePathExists(outputPath);
            using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(input));
            using var outputStream = File.Create(outputPath);
            ConvertFromStreamToStream(inputStream, outputStream);
        }
        /// <summary>
        /// Converts a human-readable file to an opcode stream.
        /// </summary>
        public static Stream ConvertFromFileToStream(string inputPath)
        {
            using var inputStream = File.OpenRead(inputPath);
            var outputStream = new MemoryStream();
            ConvertFromStreamToStream(inputStream, outputStream);
            outputStream.Position = 0;
            return outputStream;
        }
        /// <summary>
        /// Converts a human-readable string to an opcode stream.
        /// </summary>
        public static Stream ConvertFromStringToStream(string input)
        {
            using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(input));
            var outputStream = new MemoryStream();
            ConvertFromStreamToStream(inputStream, outputStream);
            outputStream.Position = 0;
            return outputStream;
        }

        private static void DoStaticSerialization(ObjectSerializer serializer, Type type, Stream serialized, Stream opcodes, TargetFormat target)
        {
            serializer.SerializeAsStatic(type, serialized);
            serialized.Seek(0, SeekOrigin.Begin);

            if (target is TargetFormat.HumanReadable)
                serialized.CopyTo(opcodes);
            else if (target is TargetFormat.FormattedHumanReadable)
                new HumanReadableIndenter().Process(serialized, opcodes);
            else
                new HumanReadableParser().Parse(serialized, opcodes);
        }
        private static void DoSerialization(ObjectSerializer serializer, object o, Stream serialized, Stream opcodes, TargetFormat target)
        {
            serializer.Serialize(o, serialized);
            serialized.Seek(0, SeekOrigin.Begin);

            if (target is TargetFormat.HumanReadable)
            {
                serialized.CopyTo(opcodes);
                opcodes.Flush();
            }
            else if (target is TargetFormat.FormattedHumanReadable)
                new HumanReadableIndenter().Process(serialized, opcodes);
            else
            {
                using MemoryStream mem = new();
                new HumanReadableParser().Parse(serialized, mem);
                mem.Position = 0;
                new OpcodeToByteCompiler().Compile(mem, opcodes);
            }
                
        }

        private static void DoDeserialization(out object? result, Type targetType, List<Instruction> instructions, WinterForgeProgressTracker? progressTracker = null)
        {
            using var executor = new InstructionExecutor();
            if (progressTracker is not null)
                executor.progressTracker = progressTracker;
            object? res = executor.Execute(instructions);

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
        internal static IList CreateList(Type t)
        {
            var list = typeof(List<>);
            var constructedListType = list.MakeGenericType(t);

            return (IList)Activator.CreateInstance(constructedListType);
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

        internal static object DeserializeFromInstructions(List<Instruction> instructions)
        {
            DoDeserialization(out object? r, typeof(object), instructions, null);
            return r;
        }
    }
}
