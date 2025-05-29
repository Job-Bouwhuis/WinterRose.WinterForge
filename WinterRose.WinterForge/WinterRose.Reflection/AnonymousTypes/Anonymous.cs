using System.Runtime.CompilerServices;

namespace WinterRose.AnonymousTypes;

/// <summary>
/// <br></br> This is also a base class for any type generated using <see cref="AnonymousTypeBuilder"/> / <see cref="AnonymousTypeReader"/>
/// and thus can be used to store these objects as class fields even
/// </summary>
public abstract class Anonymous()
{
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
    public abstract object this[string identifier]
    {
        get;
        set;
    }

    /// <summary>
    /// Gets the value using <see cref="this[string]"/> and tries to cast it to <typeparamref name="T"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="name"></param>
    /// <returns></returns>
    public T Get<T>(string name) => (T)this[name];

    /// <summary>
    /// Sets the value using <see cref="this[string]"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="name"></param>
    /// <param name="value"></param>
    public void Set<T>(string name, T value) => this[name] = value!;
}