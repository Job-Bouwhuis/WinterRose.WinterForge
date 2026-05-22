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

public class sometest
{
    public string Locale { get; private set; }
    public bool ShouldBeUsed { get; private set; }
    public bool IsDefault { get; private set; }

    public Dictionary<int, string> LEXICON { get; private set; } 
}


internal class Program
{
    public static int data = 15;
    public static bool flag = false;
    


    private static void Main()
    {
        string data = """
                sometest : global lex {
                    Locale = "en-UK";

                    ShouldBeUsed = true;
                    IsDefault = true;

                    LEXICON = <int, string>[
                        1 => "Token expired, please login again.",
                        2 => "Invalid token signature.",
                        3 => "Invalid token.",
                        4 => "Authentication failed.",
                        5 => "Invalid refresh token.",
                        6 => "Invalid credentials.",
                        7 => "Invalid current password.",
                        8 => "Email already in use.",

                        9 => "User not found.",
                        10 => "Workshop does not exist.",
                        11 => "Hoster does not exist.",
                        12 => "Group not found.",
                        13 => "Step not found.",
                        14 => "Step number mismatch.",
                        15 => "Forum not found.",
                        16 => "Session not found.",
                        17 => "You're currently not in a session.",
                        18 => "Forbidden.",

                        19 => "Saving object failed.",
                        20 => "Loading object failed.",
                        21 => "Storage key not found",

                        22 => "An unexpected error occurred.",

                        23 => "See issues for details.",
                        24 => "JSON body could not be parsed.",
                        25 => "One or more values could not be bound.",
                        26 => "One or more validation errors occurred."
                    ]

                    IsDefault = True;
                }

                return lex
                """;

        

        var tttt = WinterForge.DeserializeFromHumanReadableString(data);

        return;
        RunQuestSystemTest();
        return;

        var factory = WinterForge.CreateFactory();

        var type = factory.DefineType(typeof(TestClass));
        type.DefineMember("number", 1);
        type.DefineMember("text", "this is amazing");
        type.DefineMember("format", TargetFormat.FormattedHumanReadable);

        string result = factory.Build();
        Console.WriteLine(result);
    }

    private static void RunQuestSystemTest()
    {
        Console.WriteLine("\n===== QUEST SYSTEM TEST =====\n");

        // Create a complex quest system with nested dictionaries
        QuestSystem questSystem = CreateQuestSystem();

        Console.WriteLine("Original QuestSystem:");

        // Serialize
        Console.WriteLine("\nSerializing...");
        string serializedPath = "quest_system.wf";
        WinterForge.SerializeToFile(questSystem, serializedPath);
        Console.WriteLine($"Serialized to {serializedPath}");

        // Deserialize
        Console.WriteLine("\nDeserializing...");
        object? deserialized = WinterForge.DeserializeFromFile(serializedPath);

        if (deserialized is not QuestSystem deserializedSystem)
            throw new InvalidOperationException("Quest system test failed: deserialization resulted in wrong type.");

        Console.WriteLine("\n[QUEST SYSTEM TEST OK] All validations passed!");
        Console.WriteLine("===== QUEST SYSTEM TEST END =====\n");
    }

    private static void benchmark()
    {
        const int serializationIterations = 4;
        const int deserializationIterations = 4;

        WinterForge.CompressedStreams = true;
        WinterForge.AllowCustomCompilers = true;
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

    public static void InclusionTests()
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


    private static QuestSystem CreateQuestSystem()
    {
        var system = new QuestSystem
        {
            SystemId = "quest_v2_advanced",
            MaxActiveQuests = 10,
            QuestTitles = new Dictionary<string, string>
            {
                { "combat_01", "Goblin Slayer" },
                { "combat_02", "Dragon's End" },
                { "exploration_01", "Lost Temple" },
                { "exploration_02", "Ancient Ruins" },
                { "crafting_01", "Master Smith" }
            },
            QuestsByCategory = new Dictionary<string, Dictionary<int, string>>
            {
                {
                    "Combat", new Dictionary<int, string>
                    {
                        { 1, "Defeat 10 goblins in the forest" },
                        { 2, "Slay the dragon boss at Mount Inferno" },
                        { 3, "Clear the undead fortress" }
                    }
                },
                {
                    "Exploration", new Dictionary<int, string>
                    {
                        { 1, "Find the hidden temple in the desert" },
                        { 2, "Visit all waypoints on the map" },
                        { 3, "Discover the secret underwater cavern" }
                    }
                },
                {
                    "Crafting", new Dictionary<int, string>
                    {
                        { 1, "Craft 5 iron swords" },
                        { 2, "Create a legendary amulet" }
                    }
                }
            },
            AvailableRewards = new List<QuestReward>
            {
                new QuestReward
                {
                    RewardId = "reward_gold_100",
                    ExperiencePoints = 500,
                    ItemRewards = new Dictionary<string, int>
                    {
                        { "gold", 100 },
                        { "exp_scroll", 1 }
                    }
                },
                new QuestReward
                {
                    RewardId = "reward_gold_500",
                    ExperiencePoints = 2500,
                    ItemRewards = new Dictionary<string, int>
                    {
                        { "gold", 500 },
                        { "rare_gem", 2 },
                        { "exp_scroll", 3 }
                    }
                }
            },
            CategoryDifficulties = new Dictionary<string, int>
            {
                { "Combat", 8 },
                { "Exploration", 5 },
                { "Crafting", 3 }
            }
        };

        return system;
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

public class QuestSystem
{
    [WFInclude]
    public string SystemId { get; set; }

    [WFInclude]
    public int MaxActiveQuests { get; set; }

    [WFInclude]
    public Dictionary<string, string> QuestTitles { get; set; }

    /// <summary>
    /// Nested dictionary: Category -> (QuestId -> Description)
    /// For example: "Combat" -> { 1 -> "Defeat 10 goblins", 2 -> "Slay the dragon boss" }
    ///             "Exploration" -> { 1 -> "Find the hidden temple", 2 -> "Visit all waypoints" }
    /// </summary>
    [WFInclude]
    public Dictionary<string, Dictionary<int, string>> QuestsByCategory { get; set; }

    [WFInclude]
    public List<QuestReward> AvailableRewards { get; set; }

    [WFInclude]
    public Dictionary<string, int> CategoryDifficulties { get; set; }

    public QuestSystem()
    {
        SystemId = "quest_system_v1";
        MaxActiveQuests = 5;
        QuestTitles = new();
        QuestsByCategory = new();
        AvailableRewards = new();
        CategoryDifficulties = new();
    }
}

public class QuestReward
{
    [WFInclude]
    public string RewardId { get; set; }

    [WFInclude]
    public int ExperiencePoints { get; set; }

    [WFInclude]
    public Dictionary<string, int> ItemRewards { get; set; }

    public QuestReward()
    {
        ItemRewards = new();
    }
}

public class ComplexParserTestClass2
{
    [WFInclude]
    public string name { get; set; }

    [WFInclude]
    public List<int> numbers { get; set; }

    [WFInclude]
    public List<string> tags { get; set; }

    [WFInclude]
    public Dictionary<string, int> scores { get; set; }

    [WFInclude]
    public string format { get; set; }

    public ComplexParserTestClass2()
    {
        numbers = new();
        tags = new();
        scores = new();
    }
}