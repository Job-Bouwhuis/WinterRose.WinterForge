using System.Diagnostics;
using System.Numerics;
using System.Text;
using System.Text.Json.Serialization;
using WinterRose;
using WinterRose.Reflection;
using WinterRose.WinterForgeSerializing;
using WinterRose.WinterForgeSerializing.Compiling;
using WinterRose.WinterForgeSerializing.Containers;
using WinterRose.WinterForgeSerializing.Expressions;
using WinterRose.WinterForgeSerializing.Formatting;
using WinterRose.WinterForgeSerializing.InclusionRules;
using WinterRose.WinterForgeSerializing.Workers;

namespace WinterForgeTests;

public enum Gender
{
    Male,
    Female,
    Other
}

internal class Program
{
    public static int data = 15;
    public static bool flag = false;
    
    private unsafe static void Main()
    {
        benchmark();

        if (!File.Exists("human.txt"))
            File.Create("human.txt").Close();
        if (!File.Exists("human2.txt"))
            File.Create("human2.txt").Close();

        File.Create("bytes.wfbin").Close();

        //var tokens = ExpressionTokenizer.Tokenize("_ref(0)->X == 15");

        //Dictionary<string, string> kv = new()
        //{
        //    { "key", "val" }
        //};
        //List<demo> list = new() { demo.D(), demo.D(), demo.D(), demo.D(), demo.D(), demo.D(), demo.D(), demo.D(), demo.D(), demo.D(), demo.D(), demo.D(), demo.D(), demo.D(), demo.D(), demo.D(), demo.D(), demo.D(), demo.D(), demo.D() };

        //WinterForge.SerializeToFile(list, "human.txt", TargetFormat.HumanReadable);

        WinterForge.AllowCustomCompilers = false;
        WinterForge.ConvertFromFileToFile("human.txt", "bytes.wfbin");

        WinterForge.ConvertFromFileToFile("human.txt", "opcodesAsText.txt", TargetFormat.ReadableIntermediateRepresentation);

        WinterForgeVM.Debug = true;
        using FileStream stream = File.OpenRead("bytes.wfbin");
        var inspection = WinterForge.InspectStream(stream);
        Console.WriteLine(inspection);
    }

    private static void benchmark()
    {
        const int serializationIterations = 4;
        const int deserializationIterations = 4;

        WinterForge.CompressedStreams = true;
        WinterForge.AllowCustomCompilers = true;
        WinterRose.Windows.OpenConsole();
        Console.WriteLine("Creating data files");
        var w1 = WinterRose.WinterForgeSerializing.OverlyComplicatedTest.StupidComplexGenerator.Generate();
        Console.WriteLine("Serialization speed test...");

        Stopwatch serializationSW = new();
        int i1 = 0;

        long bestSerializationTime = long.MaxValue;
        long worstSerializationTime = long.MinValue;
        long minSerializationMemory = long.MaxValue;
        long maxSerializationMemory = long.MinValue;

        while (i1++ < serializationIterations)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long memBefore = GC.GetTotalMemory(true);

            serializationSW.Restart();
            WinterForge.SerializeToFile(w1, "Level 1");
            serializationSW.Stop();

            long memAfter = GC.GetTotalMemory(true);
            long usedMemory = memAfter - memBefore;

            long elapsed = serializationSW.ElapsedMilliseconds;
            if (elapsed < bestSerializationTime) bestSerializationTime = elapsed;
            if (elapsed > worstSerializationTime) worstSerializationTime = elapsed;

            if (usedMemory < minSerializationMemory) minSerializationMemory = usedMemory;
            if (usedMemory > maxSerializationMemory) maxSerializationMemory = usedMemory;

            Console.WriteLine($"pass {i1} - {elapsed} ms, RAM used: {usedMemory / 1024.0:N2} KB");
        }

        Console.WriteLine("\n\nDeserialization...");

        Stopwatch deserializationSW = new();
        int i2 = 0;

        long bestDeserializationTime = long.MaxValue;
        long worstDeserializationTime = long.MinValue;
        long minDeserializationMemory = long.MaxValue;
        long maxDeserializationMemory = long.MinValue;

        while (i2++ < deserializationIterations)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long memBefore = GC.GetTotalMemory(true);

            deserializationSW.Restart();
            object d = WinterForge.DeserializeFromFile("Level 1");
            deserializationSW.Stop();

            long memAfter = GC.GetTotalMemory(true);
            long usedMemory = memAfter - memBefore;

            long elapsed = deserializationSW.ElapsedMilliseconds;
            if (elapsed < bestDeserializationTime) bestDeserializationTime = elapsed;
            if (elapsed > worstDeserializationTime) worstDeserializationTime = elapsed;

            if (usedMemory < minDeserializationMemory) minDeserializationMemory = usedMemory;
            if (usedMemory > maxDeserializationMemory) maxDeserializationMemory = usedMemory;

            demo(d);
            Console.WriteLine($"pass {i2} - {elapsed} ms, RAM used: {usedMemory / 1024.0:N2} KB");
        }

        Console.WriteLine("\n\nDone!");

        Console.WriteLine("\n\nResults:");
        StringBuilder sb = new();
        sb.AppendLine($"Serialization: Best = {bestSerializationTime} ms, Worst = {worstSerializationTime} ms, RAM used: Min = {minSerializationMemory / 1024.0:N2} KB, Max = {maxSerializationMemory / 1024.0:N2} KB");
        sb.AppendLine($"Deserialization: Best = {bestDeserializationTime} ms, Worst = {worstDeserializationTime} ms, RAM used: Min = {minDeserializationMemory / 1024.0:N2} KB, Max = {maxDeserializationMemory / 1024.0:N2} KB");

        sb.AppendLine("file size (bytes): " + new FileInfo("Level 1").Length);
        using FileStream compiledFile = File.OpenRead("Level 1");
        WinterForgeStreamInfo info = WinterForge.InspectStream(compiledFile);
        sb.AppendLine(info.ToString());

        Console.WriteLine(sb.ToString());

        Console.WriteLine("Press enter to copy to clipboard and close");

        Console.ReadLine();

        WinterRose.Windows.Clipboard.WriteString(sb.ToString());

        Environment.Exit(0);
    }

    static void demo(object d) { }

    public class TextWriterStream2 : Stream
    {
        private readonly TextWriter writer;

        private readonly Encoding encoding;

        private readonly Decoder decoder;

        private readonly byte[] singleByte = new byte[1];

        public override bool CanWrite => true;

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override long Length
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public override long Position
        {
            get
            {
                throw new NotSupportedException();
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public TextWriterStream2(TextWriter writer, Encoding? encoding = null)
        {
            this.writer = writer ?? throw new ArgumentNullException("writer");
            this.encoding = encoding ?? Encoding.UTF8;
            decoder = this.encoding.GetDecoder();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            char[] array = new char[encoding.GetMaxCharCount(count)];
            int chars = decoder.GetChars(buffer, offset, count, array, 0, true);
            writer.Write(array, 0, chars);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            char[] array = new char[encoding.GetMaxCharCount(buffer.Length)];
            int chars = decoder.GetChars(buffer, array, true);
            writer.Write(array, 0, chars);
        }

        public override void WriteByte(byte value)
        {
            singleByte[0] = value;
            Write(singleByte, 0, 1);
        }

        public override void Flush()
        {
            writer.Flush();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }

    private class TestClass
    {
        public int publicField = 1; // included
        private int privateField = 2; // excluded

        public int PublicProperty { get; set; } = 3; // included
        private int PrivateProperty { get; set; } = 4; // excluded

        public static int staticPublicField = 5; // excluded
        private static int staticPrivateField = 6; // excluded

        public static int StaticPublicProperty { get; set; } = 7; // excluded
        private static int StaticPrivateProperty { get; set; } = 8; // excluded

        [WFInclude]
        public int includedField = 9; // included

        [WFInclude]
        public static int staticIncludedField = 10; // included (yes really)

        [WFInclude]
        public int IncludedProperty { get; set; } = 11; // included

        [WFInclude]
        public static int StaticIncludedProperty { get; set; } = 12; // included

        [field: WFInclude]
        private int IncludedBackingfield { get; set; } = 13; // backing field is included, property is not

        private int logicField = 5; // excluded
        public int SimpleCustomLogic // included 
        {
            get => logicField; 
            set => logicField = value;
        }

        private int logicField2 = 6; // excluded
        public int CustomLogic2 // excluded
        {
            get => logicField2 + 1; set => logicField2 = value;
        }

        private int logicField3 = 6; // excluded
        [WFInclude]
        public int CustomLogic3 // included
        {
            get => logicField3 + 1; set => logicField3 = value;
        }
    }

    public static void Run()
    {
        Console.WriteLine("=== WinterForge Inclusion Rule Test ===");

        var obj = new TestClass();
        var rh = new ReflectionHelper(typeof(TestClass));
        var members = rh.GetMembers();

        foreach (var member in members)
        {
            bool isStatic = member.IsStatic;
            bool included = isStatic
                ? InclusionRuleset.CheckStaticMember(member)
                : InclusionRuleset.CheckMember(member);

            Console.WriteLine($"[{(isStatic ? "STATIC" : "INST")}] {(included ? "✓ Included " : "Φ Excluded ")} - {member.Type,-8} - {member.Name}");
        }
    }
}

internal class ListToDictionary<TKey, TValue>
: TypeConverter<List<TKey>, Dictionary<TKey, TValue>>
{
    public override Dictionary<TKey, TValue> Convert(List<TKey> source) =>
            source.ToDictionary(key => key, key => default(TValue)!);
}

[Flags]
public enum LoveState : byte
{
    HeadOverHeels = 1,
    Infatuated = 1 << 1,
    InLove = 1 << 2,
    Heartbroken = 1 << 3,
    Single = 1 << 4,
    Complicated = 1 << 5,
    All = 0b111111
}

public class test
{
    [WFInclude]
    public LoveState state { get; set; } = LoveState.Single | LoveState.HeadOverHeels;

    public override string ToString() => $"state: {state}";
}

public class demo
{
    public Dictionary<int, List<ListClassTest<int>>> randoms;

    public demo() { }

    public static demo D()
    {
        return new demo()
        {
            randoms = new()
            {
                {new Random().Next(), listdemo.L() },
                {new Random().Next(), listdemo.L() }
            }
        };
    }
}

public class listdemo
{
    public List<ListClassTest<int>> texts;

    public static List<ListClassTest<int>> L()
    {
        listdemo d = new();
        d.texts = [];
        2.Repeat(i => d.texts.Add(ListClassTest<int>.Random()));
        return d.texts;
    }
}

public class ListClassTest<T> where T : INumber<T>
{
    public T num1;

    public T num2;

    public ListClassTest(T num1, T num2)
    {
        this.num1 = num1;
        this.num2 = num2;
    }

    public ListClassTest()
    {
        num1 = T.Zero;
        num2 = T.Zero;
    }

    public static ListClassTest<int> Random()
    {
        return new ListClassTest<int>(new Random().Next(0, 10), new Random().Next(11, 20));
    }

    public override string ToString()
    {
        return $"{num1} - {num2}";
    }
}

public enum TerrainType { Forest, Desert, Tundra, Urban }

public static class Constants
{
    public const int MAX_PARTY_SIZE = 6;
}


// ---------- Polymorphic actor hierarchy ----------
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(PlayerCharacter), "player")]
[JsonDerivedType(typeof(NpcCharacter), "npc")]
public abstract class Actor
{
    [WFInclude]
    public string Id { get; set; }
    [WFInclude]
    public string DisplayName { get; set; }
    [WFInclude]
    public int Level { get; set; }
}

public class PlayerCharacter : Actor
{
    [WFInclude]
    public Inventory Inventory { get; set; }
    [WFInclude]
    public string Faction { get; set; }
}

public class NpcCharacter : Actor
{
    [WFInclude]
    public string Disposition { get; set; }
    [WFInclude]
    public DialogueScript Script { get; set; }
}

// ---------- Supporting types ----------
public class Inventory
{
    [WFInclude]
    public int Gold { get; set; }
    [WFInclude]
    public List<ItemStack> Items { get; set; }
}

public class ItemStack
{
    [WFInclude]
    public string ItemId { get; set; }
    [WFInclude]
    public int Quantity { get; set; }
}

public class DialogueScript
{
    [WFInclude]
    public string Greeting { get; set; }
    [WFInclude]
    public Dictionary<string, string> Branches { get; set; }
}

// ---------- The world model ----------
public class Region
{
    [WFInclude]
    public string RegionId { get; set; }
    [WFInclude]
    public TerrainType Terrain { get; set; }
    [WFInclude]
    public List<Actor> Occupants { get; set; }
    [WFInclude]
    public Dictionary<string, object> Metadata { get; set; }

    // catch-all bag for unknown fields the engine might add later
    [JsonExtensionData]
    [WFInclude]
    public Dictionary<string, object> Extra { get; set; }
}

public class GameEvent
{
    [WFInclude]
    public string EventId { get; set; }
    [WFInclude]
    public DateTimeOffset Timestamp { get; set; }
    [WFInclude]
    public List<string> Tags { get; set; }
    [WFInclude]
    public Dictionary<string, object> Payload { get; set; }
}

public class GameWorld
{
    [WFInclude]
    public string WorldName { get; set; }
    [WFInclude]
    public Dictionary<string, Region> Regions { get; set; }
    [WFInclude]
    public List<GameEvent> EventQueue { get; set; }
}