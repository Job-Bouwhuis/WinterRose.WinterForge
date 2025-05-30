using System.Dynamic;
using System.Reflection.Metadata;
using WinterRose.Reflection;

namespace WinterRose.AnonymousTypes;

/// <summary>
/// A class that can read and write anonymous objects.
/// </summary>
public class AnonymousTypeReader : DynamicObject
{
    private readonly Dictionary<string, object> propertyMap = [];
    private readonly Dictionary<string, AnonymousMethod> methods = new();

    public Dictionary<string, object> DataMembers => propertyMap;

    /// <summary>
    /// The name of the class that is created from this reader when <see cref="Compile()"/> is called.
    /// </summary>
    public string? TypeName { get; set; } = null;
    /// <summary> 
    /// The base type of the generated type when <see cref="Compile()"/> is caled. <br></br>
    /// if <see langword="null"/>, its <see cref="Anonymous"/>
    /// </summary>
    public Type? BaseType { get; set; } = null;

    /// <summary>
    /// Gets the dynamic member names.
    /// </summary>
    /// <returns></returns>
    public override IEnumerable<string> GetDynamicMemberNames() => [.. propertyMap.Keys, .. methods.Keys];

    /// <summary>
    /// Creates a new empty anonymous object reader.
    /// </summary> 
    public AnonymousTypeReader()
    {
    }

    public new IEnumerable<KeyValuePair<string, object>> EnumerateProperties()
    {
        foreach (var prop in propertyMap)
        {
            yield return prop;
        }
    }

    /// <summary>
    /// Creates a new anonymous object reader from the specified object.
    /// </summary>
    /// <param name="obj"></param>
    public AnonymousTypeReader(object obj)
    {
        Read(obj);
    }

    /// <summary>
    /// Tries to get the member.
    /// </summary>
    /// <param name="binder"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    public override bool TryGetMember(GetMemberBinder binder, out object result)
    {
        var value = this[binder.Name];
        result = value.Value;
        return value.HasValue;
    }

    /// <summary>
    /// Adds a new property to the object.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="value"></param>
    public void Add(string name, object value)
    {
        propertyMap[name] = value;
    }

    /// <summary>
    /// Gets or sets a property value.
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public AnonymousValue this[string name]
    {
        get
        {
            if (propertyMap.TryGetValue(name, out var value))
                return new AnonymousValue(name, value);
            if (methods.TryGetValue(name, out var method))
                return new AnonymousValue(name, method);

            return new AnonymousValue(name, null);
        }
        set => propertyMap[name] = value;
    }

    /// <summary>
    /// Gets or sets a property value by index.
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public object this[int index]
    {
        get => propertyMap.ElementAt(index).Value;
        set => propertyMap[propertyMap.ElementAt(index).Key] = value;
    }

    /// <summary>
    /// The number of properties in the object.
    /// </summary>
    public int Count => propertyMap.Count;

    /// <summary>
    /// Gets the enumerator of all properties.
    /// </summary>
    /// <returns></returns>
    public IEnumerable<KeyValuePair<string, object>> GetEnumerator() => propertyMap;

    /// <summary>
    /// Reads an anonymous object. Overrides the current object.
    /// </summary>
    /// <param name="obj"></param>
    public void Read(object obj)
    {
        propertyMap.Clear();
        // obj is an anonymous object, so we need to use reflection to get the properties
        var properties = obj.GetType().GetProperties();

        foreach (var property in properties)
        {
            if (property.PropertyType.IsAnonymousType())
            {
                var reader = new AnonymousTypeReader();
                reader.Read(property.GetValue(obj));
                Add(property.Name, reader);
            }
            else
            {
                Add(property.Name, property.GetValue(obj));
            }
        }

        var fields = obj.GetType().GetFields();
        foreach (var field in fields)
        {
            if (field.FieldType.IsAnonymousType())
            {
                var reader = new AnonymousTypeReader();
                reader.Read(field.GetValue(obj));
                Add(field.Name, reader);
            }
            else
            {
                Add(field.Name, field.GetValue(obj));
            }
        }

        var methods = obj.GetType().GetMethods();
        foreach (var method in methods)
        {
            if (method.Name.StartsWith("set_") 
                || method.Name.StartsWith("get_") 
                || method.Name.StartsWith("add_") 
                || method.Name.StartsWith("remove_")
                || method.Name.StartsWith("op_Implicit"))
                continue;
            this.methods.Add(method.Name, new(method.Name, obj, method, method.GetParameters().Select(p => p.ParameterType).ToArray()));
        }
    }

    /// <summary>
    /// Creates an anonymous object from the specified properties.
    /// </summary>
    /// <param name="properties"></param>
    /// <returns></returns>
    public static object Compile(Dictionary<string, object> properties, string? typeName = null)
    {
        var typeBuilder = AnonymousTypeBuilder.CreateNewAnonymousType(properties, typeName);
        var obj = Activator.CreateInstance(typeBuilder);

        // Set property values
        foreach (var property in properties)
            obj.GetType().GetProperty(property.Key).SetValue(obj, property.Value);

        return obj;
    }

    /// <summary>
    /// Compiles this anonymous object into a new IL type
    /// </summary>
    /// <returns></returns>
    public object Compile()
    {
        var typeBuilder = AnonymousTypeBuilder.CreateNewAnonymousType(propertyMap, TypeName, BaseType);
        var obj = Activator.CreateInstance(typeBuilder);

        // Set property values
        foreach (var property in propertyMap)
            obj!.GetType().GetProperty(property.Key)!.SetValue(obj, property.Value);

        return obj!;
    }

    public void SetMember(string memberName, ref object memberValue)
    {
        if (propertyMap.ContainsKey(memberName))
        {
            propertyMap[memberName] = memberValue;
        }
        else
        {
            propertyMap.Add(memberName, memberValue);
        }
    }
}
