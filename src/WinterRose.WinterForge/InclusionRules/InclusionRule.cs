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

/// <summary>
/// Defines a set of rules for whether or not a field or property is to be automatically included in serialization
/// </summary>
public abstract class InclusionRule
{
    /// <summary>
    /// The weight of this rule
    /// </summary>
    public virtual int Weight => 1;

    /// <summary>
    /// When overridden in a derived class, Returns <see langword="true"/> when the member should be serialized, <see langword="false"/> if not
    /// </summary>
    /// <param name="member"></param>
    protected internal abstract InclusionVerdict ShouldInclude(MemberData member, bool staticContext);
}