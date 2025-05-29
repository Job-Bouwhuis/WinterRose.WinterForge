using System.Numerics;
using WinterRose;
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

        object an = new
        {
            X = 5,
            Y = 6,
            Z = 7,
            Name = "something",
            loc = new Vector3(1, 2, 3),
            demo = new
            {
                a = 'a'
            }
        };

        WinterForge.SerializeToFile(an, "Human.txt", TargetFormat.FormattedHumanReadable, new WinterForgeConsoleLogger(WinterForgeProgressVerbosity.Full, true));
        Console.WriteLine("\n\nSerializing ^^\nDeserializing vv\n\n");
        WinterForge.ConvertFromFileToFile("Human.txt", "opcodes.txt");
        object result = WinterForge.DeserializeFromFile("opcodes.txt", new WinterForgeConsoleLogger(WinterForgeProgressVerbosity.Full, true));

        dynamic r = result;
        Console.WriteLine(r.demo.a);
        Console.WriteLine("\n");
        Console.WriteLine(result.ToString());
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