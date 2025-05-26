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

        demo d = new demo() { test = "" };
        demo.yeet = Everything.Random();

        WinterForge.SerializeStaticToFile(typeof(demo), "opcodes.txt", 
            TargetFormat.Optimized, new WinterForgeConsoleLogger(WinterForgeProgressVerbosity.Full, true));

        demo.yeet = null;
        Console.WriteLine("\n\n");

        //WinterForge.ConvertFromFileToFile("Human.txt", "opcodes.txt");
        object result = WinterForge.DeserializeFromFile("opcodes.txt", new WinterForgeConsoleLogger(WinterForgeProgressVerbosity.Full, true));

        //Console.WriteLine(File.ReadAllText("Human.txt"));
        Console.WriteLine("\n");
        Console.WriteLine(data);
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