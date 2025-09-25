using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinterRose.WinterForgeSerializing.Util;
public static class UniqueRandomVarNameGenerator
{
    private static readonly HashSet<string> usedNames = new();
    private static readonly Random random = new();

    public static string Next
    {
        get
        {
            string name;
            do
            {
                name = GenerateRandomName();
            }
            while (!usedNames.Add(name));

            return name;
        }
    }

    private static string GenerateRandomName()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        string suffix = new string(Enumerable.Repeat(chars, 3)
            .Select(s => s[random.Next(s.Length)]).ToArray());

        int number = random.Next(100, 999);
        return $"var_{suffix}{number}";
    }
}
