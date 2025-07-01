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

        var dict1 = new Dictionary<demo, demo>();

        foreach (int i in 2)
        {
            dict1.Add(demo.D(), demo.D());
        }

        //var dict1 = new Dictionary<Type, demo>
        //{
        //    { typeof(Program), demo.D() },
        //    { typeof(Type), demo.D() },
        //    { typeof(WinterForge), demo.D() },
        //    { typeof(WinterDelegate), demo.D() }
        //};

        //List<List<string>> strings = new();
        //foreach(int i in 10)
        //{
        //    strings.Add([]);
        //    foreach(int j in 5)
        //    {
        //        strings[i].Add(Randomness.RandomString(5));
        //    }
        //}

        WinterForge.SerializeToFile(dict1, "Human.txt", TargetFormat.FormattedHumanReadable);
        WinterForge.ConvertFromFileToFile("Human.txt", "opcodes.txt");
        var result = WinterForge.DeserializeFromFile<IDictionary>("opcodes.txt");

        Console.WriteLine("\n");
    }
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