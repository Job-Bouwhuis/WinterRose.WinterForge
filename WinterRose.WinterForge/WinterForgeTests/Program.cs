using WinterRose;
using WinterRose.WinterForgeSerializing;

internal class Program
{
    public static int data = 15;

    private static void Main(string[] args)
    {
        //loading of the WinterRose library. this reference is only in the test project so that i dont have to re-create test classes. im lazy :/
        (1..2).Contains(1);

        WinterForge.ConvertFromFileToFile("Human.txt", "opcodes.txt");
        object result = WinterForge.DeserializeFromFile("opcodes.txt");

        Console.WriteLine(File.ReadAllText("Human.txt"));
        Console.WriteLine("\n");
        Console.WriteLine(data);
        Console.WriteLine(result.ToString());
    }
}

public class demo
{
    [IncludeWithSerialization]
    public string test { get; set; }

    public override string ToString() => $"test: {test ?? "null"}";
}