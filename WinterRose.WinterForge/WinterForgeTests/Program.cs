using System.Numerics;
using WinterRose;
using WinterRose.AnonymousTypes;
using WinterRose.WinterForgeSerializing;
using WinterRose.WinterForgeSerializing.Logging;
using WinterRose.WIP.TestClasses;

internal class Program
{
    public static int data = 15;

    private static void Main(string[] args)
    {
        //loading of the WinterRose library. this reference is only in the test project so that i dont have to re-create test classes. im lazy :/
        (1..2).Contains(1);

        //"Human.txt"
        //WinterForge.ConvertFromFileToFile("Human.txt", "opcodes.txt");

        object an = new
        {
            X = 5,
            Y = 2,
            nest = new
            {
                ed = 4
            },
            v = new Vector3(1, 2, 3)
        };

        WinterForge.SerializeToFile(an, "opcodes.txt", TargetFormat.Optimized, new WinterForgeConsoleLogger(WinterForgeProgressVerbosity.ClassOnly));
        Console.WriteLine("\n\nSerializing ^^\nDeserializing vv\n\n");
        Anonymous result = WinterForge.DeserializeFromFile<Anonymous>("opcodes.txt", 
            new WinterForgeConsoleLogger(WinterForgeProgressVerbosity.ClassOnly));
        int x = (int)result.Get<Anonymous>("nest")["ed"];
        Console.WriteLine("\n");
        Console.WriteLine(result.ToString());

        Type? t = TypeWorker.FindType(result.ToString());
        Console.WriteLine($"browsing findable: {t is not null}");
    }
}

public class demo
{
    public static Everything yeet;

    [IncludeWithSerialization]
    public string test { get; set; }

    public override string ToString() => $"test: {test ?? "null"}";

    [BeforeSerialize]
    public void beforeSer()
    {
        Console.WriteLine("Im gonna be serialized!");
    }

    [BeforeDeserialize]
    public void beforeDeser()
    {
        Console.WriteLine("Im going to be deserialized!");
    }

    [AfterDeserialize]
    public void afterDeser()
    {
        Console.WriteLine("I have been deserialized!");
    }
}