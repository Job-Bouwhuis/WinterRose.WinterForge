using System.Collections;
using WinterRose;
using WinterRose.AnonymousTypes;
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

        Console.WriteLine("\n\nSerializing ^^\nDeserializing vv\n\n");

        //WinterForge.SerializeToFile(an, "Human.txt", TargetFormat.FormattedHumanReadable, new WinterForgeConsoleLogger(WinterForgeProgressVerbosity.ClassOnly));

        //var dict1 = new Dictionary<int, demo>();

        //foreach(int i in 10)
        //{
        //    dict1.Add(dict1.NextAvalible(), demo.D());
        //}

        List<List<string>> strings = new();
        foreach(int i in 10)
        {
            strings.Add([]);
            foreach(int j in 5)
            {
                strings[i].Add(Randomness.RandomString(5));
            }
        }

        WinterForge.SerializeToFile(strings, "Human.txt", TargetFormat.FormattedHumanReadable);
        WinterForge.ConvertFromFileToFile("Human.txt", "opcodes.txt");
        var result = WinterForge.DeserializeFromFile<IDictionary>("opcodes.txt");

        PrintDictionary(result);

        Console.WriteLine("\n");

        void PrintDictionary(IDictionary dict)
        {
            foreach (var key in dict.Keys)
            {
                var value = dict[key];
                Console.WriteLine($"Key: {key}, Value: {value}");
            }
        }
    }

    /*
     <WinterRose.Vectors.Vector3, WinterRose.Vectors.Vector2>[
	    WinterRose.Vectors.Vector3 : 1 {
		    x = 1;
		    y = 2;
	 	    z = 3;
	    } => WinterRose.Vectors.Vector2 : 0 {
			    x = 1;
			    y = 2;
		    }
        ]
    return _stack();
     */
}

public class demo
{
    public Dictionary<int, string> randoms;

    public demo() { }

    public static demo D()
    {
        return new demo()
        {
            randoms = new()
            {
                {new Random().Next(), Randomness.RandomString(5) },
                {new Random().Next(), Randomness.RandomString(5) }
            }
        };
    }
}