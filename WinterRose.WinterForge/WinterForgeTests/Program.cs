using WinterRose.WinterForgeSerializing;
using WinterRose.WinterForgeSerializing.Workers;

internal class Program
{
    public static int data = 15;

    private static void Main(string[] args)
    {
        WinterForge.ConvertFromFileToFile("Human.txt", "opcodes.txt");
        WinterForge.DeserializeFromFile<Nothing>("opcodes.txt");
        Console.WriteLine(data);
    }
}