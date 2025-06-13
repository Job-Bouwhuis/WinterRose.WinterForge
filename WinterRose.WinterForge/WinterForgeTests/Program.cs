using System.Numerics;
using WinterRose;
using WinterRose.AnonymousTypes;
using WinterRose.Reflection;
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

        Type t = AnonymousTypeBuilder.CreateNewAnonymousType(new Dictionary<string, object>
        {
            { "test", 1 },
            { "test2", "test" },
            { "test3", new Vector2(1, 2) },
            { "list", new List<int> { 1, 2, 3, 4, 5 } }
        }, "TestAnonymous");
        Anonymous an = (Anonymous)Activator.CreateInstance(t);

        an["test"] = 1;
        an["test2"] = "test";
        an["test3"] = new Vector2(1, 2);
        an["other"] = "another value";
        an["list"] = new List<int>() { 1, 2, 3, 4, 5 };

        string serialized = WinterForge.SerializeToString(an, TargetFormat.FormattedHumanReadable);
        Anonymous an2 = WinterForge.DeserializeFromHumanReadableString<Anonymous>(serialized);
        //"Human.txt"
        //WinterForge.ConvertFromFileToFile("Human.txt", "opcodes.txt");

        //WinterForge.SerializeToFile(an, "Human.txt", TargetFormat.FormattedHumanReadable, new WinterForgeConsoleLogger(WinterForgeProgressVerbosity.ClassOnly));
        WinterForge.ConvertFromFileToFile("Human.txt", "opcodes.txt");

        //Console.WriteLine("\n\nSerializing ^^\nDeserializing vv\n\n");

        var result = WinterForge.DeserializeFromFile<demo>("opcodes.txt", 
            new WinterForgeConsoleLogger(WinterForgeProgressVerbosity.ClassOnly));

        Console.WriteLine("\n");
        Console.WriteLine(result.ToString());
    }
}

public class demo
{
    [IncludeWithSerialization]
    public string test { get; set; }

    public override string ToString() => $"test: {test ?? "null"}";

    //[BeforeSerialize]
    //public void beforeSer()
    //{
    //    Console.WriteLine("Im gonna be serialized!");
    //}

    //[BeforeDeserialize]
    //public void beforeDeser()
    //{
    //    Console.WriteLine("Im going to be deserialized!");
    //}

    //[AfterDeserialize]
    //public void afterDeser()
    //{
    //    Console.WriteLine("I have been deserialized!");
    //}
}