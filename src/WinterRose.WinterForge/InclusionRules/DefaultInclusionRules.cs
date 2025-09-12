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
