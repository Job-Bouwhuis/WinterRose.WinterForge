//using System;
//using System.Collections.Generic;

//namespace WinterRose.Reflection;

//public class MemberDataCollection<T>
//{
//    private readonly Dictionary<string, MemberData> members;

//    public MemberDataCollection(ReflectionHelper<T> rh)
//    {
//        members = rh.GetMembers().members;
//    }

//    public MemberDataCollection(List<MemberData> members)
//    {
//        this.members = members.ToDictionary(m => m.Name);
//    }

//    /// <summary>
//    /// Gets the member with the provided name.
//    /// </summary>
//    /// <param name="name"></param>
//    /// <returns>The found member</returns>
//    /// <exception cref="FieldNotFoundException"></exception>
//    public MemberData this[string name] => members[name];

//    /// <summary>
//    /// Gets the <see cref="IEnumerator{T}"/> for this collection
//    /// </summary>
//    /// <returns></returns>
//    public IEnumerator<MemberData> GetEnumerator() => members.Values.GetEnumerator();

//    internal bool TryGet(string name, out MemberData? member)
//    {
//        try
//        {
//            return (member = this[name]) != null;
//        }
//        catch
//        {
//            member = null;
//            return false;
//        }
//    }
//}
