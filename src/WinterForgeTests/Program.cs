using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using WinterRose;
using WinterRose.AnonymousTypes;
using WinterRose.ForgeGuardChecks;
using WinterRose.Reflection;
using WinterRose.Vectors;
using WinterRose.WinterForgeSerializing;
using WinterRose.WinterForgeSerializing.Logging;

namespace WinterForgeTests;

internal class Program
{
    public static int data = 15;

    private static void Main()
    {
        WinterForge.GlobalAccessRestriction = WinterForgeGlobalAccessRestriction.AllAccessing;

        string opcodes = WinterForge.ConvertFromStringToString("""
            Program->data = 10;
            """);

        var attack = WinterForge.DeserializeFromString<Anonymous>(opcodes);
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
    [IncludeWithSerialization]
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
    [IncludeWithSerialization]
    public string Id { get; set; }
    [IncludeWithSerialization]
    public string DisplayName { get; set; }
    [IncludeWithSerialization]
    public int Level { get; set; }
}

public class PlayerCharacter : Actor
{
    [IncludeWithSerialization]
    public Inventory Inventory { get; set; }
    [IncludeWithSerialization]
    public string Faction { get; set; }
}

public class NpcCharacter : Actor
{
    [IncludeWithSerialization]
    public string Disposition { get; set; }
    [IncludeWithSerialization]
    public DialogueScript Script { get; set; }
}

// ---------- Supporting types ----------
public class Inventory
{
    [IncludeWithSerialization]
    public int Gold { get; set; }
    [IncludeWithSerialization]
    public List<ItemStack> Items { get; set; }
}

public class ItemStack
{
    [IncludeWithSerialization]
    public string ItemId { get; set; }
    [IncludeWithSerialization]
    public int Quantity { get; set; }
}

public class DialogueScript
{
    [IncludeWithSerialization]
    public string Greeting { get; set; }
    [IncludeWithSerialization]
    public Dictionary<string, string> Branches { get; set; }
}

// ---------- The world model ----------
public class Region
{
    [IncludeWithSerialization]
    public string RegionId { get; set; }
    [IncludeWithSerialization]
    public TerrainType Terrain { get; set; }
    [IncludeWithSerialization]
    public List<Actor> Occupants { get; set; }
    [IncludeWithSerialization]
    public Dictionary<string, object> Metadata { get; set; }

    // catch-all bag for unknown fields the engine might add later
    [JsonExtensionData]
    [IncludeWithSerialization]
    public Dictionary<string, object> Extra { get; set; }
}

public class GameEvent
{
    [IncludeWithSerialization]
    public string EventId { get; set; }
    [IncludeWithSerialization]
    public DateTimeOffset Timestamp { get; set; }
    [IncludeWithSerialization]
    public List<string> Tags { get; set; }
    [IncludeWithSerialization]
    public Dictionary<string, object> Payload { get; set; }
}

public class GameWorld
{
    [IncludeWithSerialization]
    public string WorldName { get; set; }
    [IncludeWithSerialization]
    public Dictionary<string, Region> Regions { get; set; }
    [IncludeWithSerialization]
    public List<GameEvent> EventQueue { get; set; }
}