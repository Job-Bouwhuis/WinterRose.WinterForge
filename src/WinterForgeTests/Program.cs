using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Drawing;
using System.Dynamic;
using System.Numerics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using WinterRose;
using WinterRose.AnonymousTypes;
using WinterRose.FileManagement;
using WinterRose.ForgeGuardChecks;
using WinterRose.Reflection;
using WinterRose.WinterForgeSerializing;
using WinterRose.WinterForgeSerializing.Compiling;
using WinterRose.WinterForgeSerializing.Expressions;
using WinterRose.WinterForgeSerializing.Formatting;
using WinterRose.WinterForgeSerializing.InclusionRules;
using WinterRose.WinterForgeSerializing.Logging;

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
        if (!File.Exists("human.txt"))
            File.Create("human.txt").Close();
        if (!File.Exists("human2.txt"))
            File.Create("human2.txt").Close();

        File.Create("bytes.wfbin").Close();

        var tokens = ExpressionTokenizer.Tokenize("_ref(0)->X == 15");

        //Dictionary<string, string> kv = new()
        //{
        //    { "key", "val" }
        //};
        List<demo> list = new() { demo.D(), demo.D(), demo.D(), demo.D(), demo.D(), demo.D(), demo.D(), demo.D(), demo.D(), demo.D(), demo.D(), demo.D(), demo.D(), demo.D(), demo.D(), demo.D(), demo.D(), demo.D(), demo.D(), demo.D() };

        //WinterForge.SerializeToFile(list, "human.txt", TargetFormat.HumanReadable);

        WinterForge.ConvertFromFileToFile("human.txt", "bytes.wfbin");

        var vec = WinterForge.DeserializeFromFile("bytes.wfbin");
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