using System.Numerics;
using System.Runtime.InteropServices;
using WinterForgeTests;
using WinterRose.ForgeGuardChecks;
using WinterRose.ForgeGuardChecks.Expectations;
using WinterRose.WinterForgeSerializing;

[GuardClass("WinterForge Syntax Tests")]
public class WinterForgeSyntaxTests
{
    [GuardSetup]
    public static void GuardWideSetup()
    {

    }

    [BeforeEach]
    public void SetupBeforeEachGuard()
    {

    }

    [AfterEach]
    public void TeardownAfterEachGuard()
    {

    }

    [GuardTeardown]
    public static void GuardWideTeardown()
    {
    }

    [Guard(Severity.Minor)]
    public void InstanceCreation_DefaultCtor()
    {
        string wfCode = @"
            System.Numerics.Vector2 : 0;
            return 0;
        ";

        var result = WinterForge.DeserializeFromHumanReadableString<Vector2>(wfCode);
        Forge.Expect(result).Not.Null();
        Forge.Expect(result).OfType(typeof(Vector2));

        void temp()
        {
            throw new NotImplementedException();
        }

        Forge.Expect(temp).WhenCalled().ToThrow<NotImplementedException>();
    }

    [Guard]
    public void InstanceCreation_ParamCtorAndBlock()
    {
        string wfCode = @"
            System.Numerics.Vector2(3, 4) : 0 {
                X = 10;
            }
            return 0;
        ";

        var result = WinterForge.DeserializeFromHumanReadableString<Vector2>(wfCode);
        Forge.Expect(result).Not.Null();
        Forge.Expect(result).OfType(typeof(Vector2));
        Forge.Expect(result.X).EqualTo(10);
        Forge.Expect(result.Y).EqualTo(4);
    }

    [Guard]
    public void ListCollection_TopLevelAndField()
    {
        string wfCode = @"
            <int>[1, 2, 3, 4, 5]
            return _stack();
        ";

        var result = WinterForge.DeserializeFromHumanReadableString<List<int>>(wfCode);
        Forge.Expect(result).Not.Null();
        Forge.Expect(result).OfType(typeof(List<int>));
        Forge.Expect(result.Count).EqualTo(5);
        Forge.Expect(result[2]).EqualTo(3);
    }

    [Guard]
    public void DictionaryCollection_BasicSyntax()
    {
        string wfCode = @"
            <int, string>[
                1 => ""one"",
                2 => ""two""
            ]
            return _stack();
        ";

        var result = WinterForge.DeserializeFromHumanReadableString<Dictionary<int, string>>(wfCode);
        Forge.Expect(result).Not.Null();
        Forge.Expect(result).OfType(typeof(Dictionary<int, string>));
        Forge.Expect(result[1]).EqualTo("one");
        Forge.Expect(result[2]).EqualTo("two");
    }

    [Guard]
    public void StaticAndInstanceAccess_Chained()
    {
        string wfCode = @"
            Program->data = 15;
            System.Numerics.Vector2 : 0;
            _ref(0)->X = 7;
            return 0;
        ";

        var result = WinterForge.DeserializeFromHumanReadableString<Vector2>(wfCode);
        Forge.Expect(result).OfType(typeof(Vector2));
        Forge.Expect(result.X).EqualTo(7);
        Forge.Expect(Program.data).EqualTo(15);
    }

    [Guard]
    public void Aliasing_Usage()
    {
        string wfCode = @"
            System.Numerics.Vector2 : 0;
            alias 0 as vec;
            vec->X = 5;
            return 0;
        ";

        var result = WinterForge.DeserializeFromHumanReadableString<Vector2>(wfCode);
        Forge.Expect(result).OfType(typeof(Vector2));
        Forge.Expect(result.X).EqualTo(5);
    }

    [Guard]
    public void Return_StackUsage()
    {
        string wfCode = @"
            <int>[1, 2, 3]
            return _stack();
        ";

        var result = WinterForge.DeserializeFromHumanReadableString<List<int>>(wfCode);
        Forge.Expect(result).OfType(typeof(List<int>));
        Forge.Expect(result.Count).EqualTo(3);
    }
}
