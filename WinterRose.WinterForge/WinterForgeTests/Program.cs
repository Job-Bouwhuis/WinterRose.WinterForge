using System.Collections;
using System.Numerics;
using System.Text.Encodings.Web;
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

    private static void Main(string[] args)
    {
        //loading of the WinterRose library. this reference is only in the test project so that i dont have to re-create test classes. im lazy :/
        (1..2).Contains(1);

        Tests().GetAwaiter().GetResult();
    }

    private static async Task Tests()
    {
        //var dict1 = new Dictionary<int, LoveState>();

        //foreach (int i in 2)
        //    dict1.Add(dict1.NextAvalible(), LoveState.Single);

        var dict1 = new test();

        await WinterForge.SerializeToFileAsync(dict1, "Human.txt", TargetFormat.FormattedHumanReadable);
        WinterForge.ConvertFromFileToFile("Human.txt", "opcodes.txt");
        var task = WinterForge.DeserializeFromFileAsync<test>("opcodes.txt");
        var result = await task;
        
        Console.WriteLine(result?.ToString() ?? "null");
    }
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
    All = HeadOverHeels | Infatuated | InLove | Heartbroken | Single | Complicated
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