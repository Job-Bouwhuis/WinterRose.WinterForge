using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using WinterRose.Reflection;

namespace WinterRose.AnonymousTypes;

/// <summary>
/// <br></br> This is also a base class for any type generated using <see cref="AnonymousTypeBuilder"/> / <see cref="AnonymousTypeReader"/>
/// and thus can be used to store these objects as class fields even
/// <br></br><br></br>
/// Can also be used on its own to store runtime variables in a dictionary-like manner.
/// 
/// 
/// </summary>
/// <remarks>This class inherits <see cref="DynamicObject"/></remarks>
public class Anonymous() : DynamicObject
{
    internal Dictionary<string, object?> runtimeVariables = new Dictionary<string, object?>();

    /// <summary>
    /// Accesses the anonymous object to get or set the field of the given name.
    /// <br></br> throws an exception if the identifier doesnt exist
    /// </summary>
    /// <remarks>The implementation is generated when the anonymous type <br></br>
    /// is compiled using <see cref="AnonymousTypeBuilder"/> / <see cref="AnonymousTypeReader"/><br></br><br></br>
    /// 
    /// This does not use any reflection or use of the dynamic keyword and is therefor really fast</remarks>
    /// <param name="identifier"></param>
    /// <returns></returns>
    public virtual object? this[string identifier]
    {
        get
        {
            if (runtimeVariables.TryGetValue(identifier, out var value))
                return value;

            // last ditch effort to find the member using reflection
            var member = new ReflectionHelper(this).GetMember(identifier);
            if(member is not null)
                return member.GetValue(this);
            throw new AnonymousFieldDoesntExistException(identifier);
        }
        set
        {
            runtimeVariables[identifier] = value;
        }
    }

    public bool TryGet<T>(string identifier, out T? val)
    {
        val = default;
        try
        {
            object? o = this[identifier];
            if(o is null)
            {
                val = (T?)o;
                return true;
            }
            if (!o.GetType().IsAssignableTo(typeof(T)))
                return false;
            val = (T)o;
            return true;
        }
        catch (AnonymousFieldDoesntExistException)
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the value using <see cref="this[string]"/> and tries to cast it to <typeparamref name="T"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="name"></param>
    /// <returns></returns>
    public T? Get<T>(string name) => (T?)this[name];

    /// <summary>
    /// Sets the value using <see cref="this[string]"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="name"></param>
    /// <param name="value"></param>
    public void Set<T>(string name, T? value) => this[name] = value;

    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        result = this[binder.Name];
        return true;
    }

    public override bool TrySetMember(SetMemberBinder binder, object? value)
    {
        this[binder.Name] = value;
        return true;
    }

    public override IEnumerable<string> GetDynamicMemberNames() => [.. new ReflectionHelper(this).GetMembers()
            .Where(m => m.Name is not "runtimeVariables" and not "Item")
            .Select(m => m.Name)];
}