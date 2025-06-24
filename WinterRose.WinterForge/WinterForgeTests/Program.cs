using System.Collections;
using System.Text.Encodings.Web;
using WinterRose;
using WinterRose.AnonymousTypes;
using WinterRose.ForgeGuardChecks;
using WinterRose.Reflection;
using WinterRose.Vectors;
using WinterRose.WinterForgeSerializing;
using WinterRose.WinterForgeSerializing.Logging;
using WinterRose.WIP.TestClasses;

namespace WinterForgeTests;

internal class Program
{
    public static int data = 15;

    private static void Main(string[] args)
    {
        //loading of the WinterRose library. this reference is only in the test project so that i dont have to re-create test classes. im lazy :/
        (1..2).Contains(1);

        var dict1 = new Dictionary<int, demo>();

        foreach (int i in 10)
        {
            dict1.Add(dict1.NextAvalible(), demo.D());
        }

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
    public Dictionary<int, List<ListClassTest>> randoms;

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
    public List<ListClassTest> texts;

    public static List<ListClassTest> L()
    {
        listdemo d = new();
        d.texts = [];
        2.Repeat(i => d.texts.Add(ListClassTest.Random()));
        return d.texts;
    }
}