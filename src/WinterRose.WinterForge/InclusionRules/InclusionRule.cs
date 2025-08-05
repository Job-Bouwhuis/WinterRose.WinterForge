using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using WinterRose.Reflection;
using WinterRose.WinterForgeSerializing.Util;

namespace WinterRose.WinterForgeSerializing.InclusionRules;

public enum InclusionVerdict
{
    DenySoft = 0,
    DenyHard = 1,
    AllowSoft = 2,
    AllowHard = 3
}

/// <summary>
/// Defines a set of rules for whether or not a field or property is to be automatically included in serialization
/// </summary>
public abstract class InclusionRule
{
    /// <summary>
    /// When overridden in a derived class, Returns <see langword="true"/> when the member should be serialized, <see langword="false"/> if not
    /// </summary>
    /// <param name="member"></param>
    protected internal abstract InclusionVerdict ShouldInclude(MemberData member, bool staticContext);
}

public class DefaultInclusionRules : InclusionRule
{
    protected internal override InclusionVerdict ShouldInclude(MemberData member, bool staticContext)
    {
        if (!staticContext && member.IsStatic)
            return ValidateIncludeAttributes(member);

        if (member.MemberType == MemberTypes.Field)
        {
            if (member.IsPublic)
                return InclusionVerdict.AllowSoft;
            return ValidateIncludeAttributes(member);
        }

        if (member.MemberType == MemberTypes.Property)
        {
            var propKind = PropertyKindCache.GetPropertyKind(member.DeclaringType, member);

            if (propKind == PropertyKind.Auto && member.IsPublic)
                return InclusionVerdict.AllowSoft;
            return ValidateIncludeAttributes(member);
        }

        return InclusionVerdict.DenySoft;

        static InclusionVerdict ValidateIncludeAttributes(MemberData member)
        {
            if (member.Attributes.Any(x => x is WFIncludeAttribute or DataMemberAttribute))
                return InclusionVerdict.AllowSoft;
            else
                return InclusionVerdict.DenySoft;
        }
    }
}