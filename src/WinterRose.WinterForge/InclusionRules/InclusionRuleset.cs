using System.Runtime.Serialization;
using System.Transactions;
using WinterRose.Reflection;

namespace WinterRose.WinterForgeSerializing.InclusionRules;

/// <summary>
/// Acts as the central point of where the inclusion rules are stored and called
/// </summary>
public static class InclusionRuleset
{
    private static List<InclusionRule> rules = [];

    static InclusionRuleset()
    {
        rules.Add(new DefaultInclusionRules());
    }

    public static void AddRule<T>(T rule) where T : InclusionRule
    {
        if(rules.Any(r => r is T)) return;
        rules.Add(rule);
    }

    public static void RemoveRule<T>() => rules.RemoveAll(r => r is T);
    public static InclusionRule? GetRule<T>() where T : InclusionRule => rules.FirstOrDefault(r => r is T);
    public static void AddOrReplaceRule<T>(T rule) where T : InclusionRule
    {
        RemoveRule<T>();
        rules.Add(rule);
    }

    public static Action<MemberData, string>? LogInclusionDecision;

    public static bool CheckMember(MemberData member) =>
        EvaluateMember(member, staticContext: false);

    public static bool CheckStaticMember(MemberData member) =>
        EvaluateMember(member, staticContext: true);

    private static bool EvaluateMember(MemberData member, bool staticContext)
    {
        int internalCheck = CheckInternal(member);
        if (internalCheck == -1) return false;
        if (internalCheck == 1) return true;

        if (rules.Count == 1)
            return rules[0].ShouldInclude(member, staticContext) is InclusionVerdict.AllowSoft or InclusionVerdict.AllowHard;

        int allowSoftScore = 0;
        int denySoftScore = 0;

        foreach (var rule in rules)
        {
            var verdict = rule.ShouldInclude(member, staticContext);

            switch (verdict)
            {
                case InclusionVerdict.DenyHard:
                    LogInclusionDecision?.Invoke(member, $"DenyHard received by {rule.GetType().Name} — excluding member");
                    return false;

                case InclusionVerdict.AllowHard:
                    LogInclusionDecision?.Invoke(member, $"AllowHard received by {rule.GetType().Name} — including member");
                    return true;

                case InclusionVerdict.AllowSoft:
                    allowSoftScore += rule.Weight;
                    break;

                case InclusionVerdict.DenySoft:
                    denySoftScore += rule.Weight;
                    break;
            }

            LogInclusionDecision?.Invoke(member, $"Rule {rule.GetType().Name} voted {verdict}");
        }

        bool result = allowSoftScore > denySoftScore;

        LogInclusionDecision?.Invoke(member, 
            $"Final vote scores: {allowSoftScore} soft-allow vs {denySoftScore} soft-deny → {(result ? "Included" : "Excluded")}");

        return result;
    }


    private static int CheckInternal(MemberData member)
    {
        if (member is AnonymousMember)
            return 1;

        if (member.Type.IsAssignableTo(typeof(Delegate)))
            return -1; // delegates may not be serialzied

        if (member.Attributes.FirstOrDefault(x =>
                x is WFExcludeAttribute
                or NonSerializedAttribute
                or IgnoreDataMemberAttribute) != null)
            return -1; // early exit if it has an exclude attribute

        if (!member.CanWrite)
            return -1;

        return 0;
    }
}
