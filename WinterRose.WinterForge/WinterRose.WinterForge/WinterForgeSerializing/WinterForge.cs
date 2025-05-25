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
using WinterRose.WinterForgeSerializing.Formatting;
using WinterRose.WinterForgeSerializing.Logging;
using WinterRose.WinterForgeSerializing.Workers;

namespace WinterRose.WinterForgeSerializing
{
    /// <summary>
    /// The main delegation class for the WinterForge serialization system
    /// </summary>
    public static class WinterForge
    {
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

        /// <summary>
        /// Serializes the given object directly to opcodes for fastest deserialization
        /// </summary>
        /// <param name="o"></param>
        /// <param name="path"></param>
        public static void SerializeToFile(object o, string path, TargetFormat targetFormat = TargetFormat.Optimized, WinterForgeProgressTracker? progressTracker = null)
        {
            List<string> paths = path.ToString().Split(['/', '\\']).ToList();
            if (paths.Count > 1)
            {
                paths.RemoveAt(paths.Count - 1);

                string directory = string.Join("/", paths);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

            }

            using (Stream serialized = new MemoryStream())
            using (Stream opcodes = File.Open(path, FileMode.Create, FileAccess.ReadWrite))
            //using (Stream formatted = File.OpenWrite("lasthumanreadable.txt"))
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
        /// Deserializes from the given stream in which opcodes should exist
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        public static object DeserializeFromStream(Stream stream, WinterForgeProgressTracker? progressTracker = null)
        {
            var instr = InstructionParser.ParseOpcodes(stream);
            return DoDeserialization(typeof(Nothing), instr, progressTracker);
        }
        /// <summary>
        /// Deserializes from the given stream in which opcodes should exist
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="stream"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        public static T DeserializeFromStream<T>(Stream stream, WinterForgeProgressTracker? progressTracker = null)
        {
            return (T)DeserializeFromStream(stream, progressTracker);
        }
        /// <summary>
        /// Deserializes from the given string of opcodes
        /// </summary>
        /// <param name="opcodes"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        public static object DeserializeFromString(string opcodes, Encoding? encoding = null)
        {
            using MemoryStream ops = new MemoryStream((encoding ?? Encoding.UTF8).GetBytes(opcodes));
            return DeserializeFromStream(ops);
        }
        /// <summary>
        /// Deserializes from the given string of opcodes
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="opcodes"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        public static T DeserializeFromString<T>(string opcodes, Encoding? encoding = null)
        {
            return (T)DeserializeFromString(opcodes, encoding);
        }
        /// <summary>
        /// Deserializes from the given human readable string
        /// </summary>
        /// <param name="humanReadable"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        public static object DeserializeFromHumanReadableString(string humanReadable, WinterForgeProgressTracker? progressTracker = null)
        {
            using var opcodes = new MemoryStream();
            using var serialized = new MemoryStream();

            byte[] humanBytes = Encoding.UTF8.GetBytes(humanReadable);
            serialized.Write(humanBytes, 0, humanBytes.Length);
            serialized.Seek(0, SeekOrigin.Begin);

            new HumanReadableParser().Parse(serialized, opcodes);
            opcodes.Seek(0, SeekOrigin.Begin);

            var instructions = InstructionParser.ParseOpcodes(opcodes);
            return DoDeserialization(typeof(Nothing), instructions, progressTracker);
        }
        /// <summary>
        /// 
        /// Deserializes from the given human readable string
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="humanReadable"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        public static T DeserializeFromHumanReadableString<T>(string humanReadable, WinterForgeProgressTracker? progressTracker = null)
        {
            return (T)DeserializeFromHumanReadableString(humanReadable, progressTracker);
        }
        /// <summary>
        /// Deserializes from the given human readable stream
        /// </summary>
        /// <param name="humanReadable"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        public static object DeserializeFromHumanReadableStream(Stream humanReadable, WinterForgeProgressTracker? progressTracker = null)
        {
            using var opcodes = new MemoryStream();

            new HumanReadableParser().Parse(humanReadable, opcodes);
            opcodes.Seek(0, SeekOrigin.Begin);

            var instructions = InstructionParser.ParseOpcodes(opcodes);
            return DoDeserialization(typeof(Nothing), instructions, progressTracker);
        }
        /// <summary>
        /// 
        /// Deserializes from the given human readable stream
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="humanReadable"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        public static T DeserializeFromHumanReadableStream<T>(Stream humanReadable, WinterForgeProgressTracker? progressTracker = null)
        {
            return (T)DeserializeFromHumanReadableStream(humanReadable, progressTracker);
        }
        /// <summary>
        /// Deserialies from the given file that has the human readable format
        /// </summary>
        /// <param name="path"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        public static object DeserializeFromHumanReadableFile(string path, WinterForgeProgressTracker? progressTracker = null)
        {
            using var opcodes = new MemoryStream();
            using var humanReadable = File.OpenRead(path);

            new HumanReadableParser().Parse(humanReadable, opcodes);
            opcodes.Seek(0, SeekOrigin.Begin);

            var instructions = InstructionParser.ParseOpcodes(opcodes);
            return DoDeserialization(typeof(Nothing), instructions, progressTracker);
        }
        /// <summary>
        /// Deserialies from the given file that has the human readable format
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="path"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        public static T DeserializeFromHumanReadableFile<T>(string path, WinterForgeProgressTracker? progressTracker = null)
        {
            return (T)DeserializeFromHumanReadableFile(path, progressTracker);
        }
        /// <summary>
        /// Deserializes from the file that has the opcodes
        /// </summary>
        /// <param name="path"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        public static object DeserializeFromFile(string path, WinterForgeProgressTracker? progressTracker = null)
        {
            using Stream opcodes = File.OpenRead(path);
            var instructions = InstructionParser.ParseOpcodes(opcodes);
            return DoDeserialization(typeof(Nothing), instructions, progressTracker);
        }
        /// <summary>
        /// Deserializes from the file that has the opcodes
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="path"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        public static T DeserializeFromFile<T>(string path, WinterForgeProgressTracker? progressTracker = null)
        {
            return (T)DeserializeFromFile(path, progressTracker);
        }

        /// <summary>
        /// Converts the human readable format into opcodes
        /// </summary>
        /// <param name="humanreadable"></param>
        /// <param name="opcodeDestination"></param>
        public static void ConvertHumanReadable(Stream humanreadable, Stream opcodeDestination) 
            => new HumanReadableParser().Parse(humanreadable, opcodeDestination);
        /// <summary>
        /// Converts a human-readable file to an opcode file.
        /// </summary>
        public static void ConvertFromFileToFile(string inputPath, string outputPath)
        {
            using var input = File.OpenRead(inputPath);
            using var output = File.Create(outputPath);
            ConvertHumanReadable(input, output);
        }
        /// <summary>
        /// Converts a human-readable string to an opcode string.
        /// </summary>
        public static string ConvertFromStringToString(string input)
        {
            using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(input));
            using var outputStream = new MemoryStream();
            ConvertHumanReadable(inputStream, outputStream);
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
            ConvertHumanReadable(inputStream, outputStream);
            outputStream.Position = 0;
            using var reader = new StreamReader(outputStream);
            return reader.ReadToEnd();
        }
        /// <summary>
        /// Converts a human-readable string to an opcode file.
        /// </summary>
        public static void ConvertFromStringToFile(string input, string outputPath)
        {
            using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(input));
            using var outputStream = File.Create(outputPath);
            ConvertHumanReadable(inputStream, outputStream);
        }
        /// <summary>
        /// Converts a human-readable file to an opcode stream.
        /// </summary>
        public static MemoryStream ConvertFromFileToStream(string inputPath)
        {
            using var inputStream = File.OpenRead(inputPath);
            var outputStream = new MemoryStream();
            ConvertHumanReadable(inputStream, outputStream);
            outputStream.Position = 0;
            return outputStream;
        }
        /// <summary>
        /// Converts a human-readable string to an opcode stream.
        /// </summary>
        public static MemoryStream ConvertFromStringToStream(string input)
        {
            using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(input));
            var outputStream = new MemoryStream();
            ConvertHumanReadable(inputStream, outputStream);
            outputStream.Position = 0;
            return outputStream;
        }


        private static void DoSerialization(ObjectSerializer serializer, object o, Stream serialized, Stream opcodes, TargetFormat target)
        {
            serializer.Serialize(o, serialized);
            serialized.Seek(0, SeekOrigin.Begin);

            if (target is TargetFormat.HumanReadable)
                serialized.CopyTo(opcodes);
            else if (target is TargetFormat.FormattedHumanReadable)
                new HumanReadableIndenter().Process(serialized, opcodes);
            else
                new HumanReadableParser().Parse(serialized, opcodes);
        }
        private static object DoDeserialization(Type targetType, List<Instruction> instructions, WinterForgeProgressTracker? progressTracker = null)
        {
            using var executor = new InstructionExecutor();
            if (progressTracker is not null)
                executor.progressTracker = progressTracker;
            object res = executor.Execute(instructions);

            if (res is List<object> list)
            {
                if (targetType.IsArray)
                {
                    var array = Array.CreateInstance(targetType.GetElementType()!, list.Count);

                    for (int i = 0; i < list.Count; i++)
                        array.SetValue(list[i], i);

                    return array;
                }

                if (targetType.Name.Contains("List`1"))
                {
                    var targetList = CreateList(targetType.GetGenericArguments()[0]);

                    for (int i = 0; i < list.Count; i++)
                        targetList.Add(list[i]);

                    return targetList;
                }

                throw new Exception("invalid deserialization!");
            }

            return res;
        }
        internal static IList CreateList(Type t)
        {
            var list = typeof(List<>);
            var constructedListType = list.MakeGenericType(t);

            return (IList)Activator.CreateInstance(constructedListType);
        }
    }
}
