namespace WinterRose.WinterForgeSerializing;

/// <summary>
/// Generalized block state that overrules any <see cref="AccessFilter"/> <br></br>
/// All settings are global and do not strictly override <see cref="AccessFilter"/> they merely act on top of that.
/// <br></br><br></br> EG: <br></br>
/// <see cref="StaticAndInstanceVariableOnly"/> blocks all method calls, but allows variable accesses.
/// but if a <see cref="AccessFilter"/> exists blocking a variable, its still blocked
/// </summary>
/// <remarks>NOTE: using this may restrict the usage of serializing static types / members</remarks>
public enum WinterForgeGlobalAccessRestriction
{
    /// <summary>
    /// Any and all accessing is blocked (syntax using ->)
    /// <br></br> Serializing statics is not possible with this value
    /// </summary>
    AllAccessing,
    /// <summary>
    /// Any and all accessing on static types is blocked.
    /// <br></br> Serializing statics is not possible with this value
    /// </summary>
    InstanceOnly,
    /// <summary>
    /// Any and all accessing on statics and instance methods is blocked
    /// <br></br> Serializing statics is not possible with this value
    /// </summary>
    InstanceVariablesOnly,
    /// <summary>
    /// Any and all method calls both static and instance are blocked.
    /// <br></br> Serializing statics is possible with this value
    /// </summary>
    StaticAndInstanceVariableOnly,
    /// <summary>
    /// no global block rule. everything is dictated by the <see cref="AccessFilter"/>
    /// </summary>
    NoGlobalBlock
}