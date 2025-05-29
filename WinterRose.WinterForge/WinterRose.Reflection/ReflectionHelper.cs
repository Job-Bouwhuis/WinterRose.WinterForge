using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using WinterRose.Reflection;

namespace WinterRose.Reflection
{
    /// <summary>
    /// Provides helper functions
    /// </summary>
    public class ReflectionHelper : DynamicObject
    {
        public static ReflectionHelper ForObject(ref object o) => new(ref o);
        public static ReflectionHelper ForObject(object o) => new(ref o);

        public static ReflectionHelper ForType(Type type) => new(type);

        private readonly static BindingFlags withPrivateFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static readonly BindingFlags noPrivateFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
        private BindingFlags flags = withPrivateFlags;
        /// <summary>
        /// Explicitly set that private fields should be included
        /// </summary>
        public bool IncludePrivateFields
        {
            get => flags.HasFlag(BindingFlags.NonPublic);
            set
            {
                if (value)
                    flags = withPrivateFlags;
                else
                    flags = noPrivateFlags;
            }
        }
        object obj;
        public Type ObjectType { get; init; }

        public ReflectionHelper(ref object obj)
        {
            this.obj = obj;
            ObjectType = obj.GetType();
        }
        public ReflectionHelper(Type objType)
        {
            this.ObjectType = objType;
        }

        public MemberData GetMember(string name)
        {
            int res = GetFieldOrProperty(name, out var field, out var property);
            if (res is -1)
                throw new FieldNotFoundException($"field or property with name '{name}' does not exist");
            if (res is 0)
                return field;
            if (res is 1)
                return property;
            return null;
        }
        public List<MemberData> GetMembers()
        {
            List<MemberData> members = [.. ObjectType.GetFields(flags), .. ObjectType.GetProperties(flags)];
            return [.. members.Where(x => x.IsValid)];

        }
        public List<FieldInfo> GetFields() => ObjectType.GetFields(flags).ToList();
        public List<PropertyInfo> GetProperties() => ObjectType.GetProperties(flags).ToList();

        public FieldInfo? GetField(string name) => ObjectType.GetField(name, flags);
        public PropertyInfo? GetProperty(string name) => ObjectType.GetProperty(name, flags);

        public override bool TryGetMember(GetMemberBinder binder, out object? result)
        {
            result = null;
            try
            {
                result = GetValueFrom(binder.Name);
            }
            catch
            {
                return false;
            }
            
            return true;
        }

        public MethodInfo GetMethod(string method) => ObjectType.GetMethod(method, flags);

        public override bool TrySetMember(SetMemberBinder binder, object? value)
        {
            SetValue(binder.Name, value);
            return true;
        }

        /// <summary>
        /// Gets a field or a property with the given name
        /// </summary>
        /// <param name="name"></param>
        /// <param name="field"></param>
        /// <param name="property"></param>
        /// <returns>0 if a field was found, 1 if a property was found, and -1 if neither was found</returns>
        public int GetFieldOrProperty(string name, out FieldInfo? field, out PropertyInfo? property)
        {
            field = GetField(name);
            property = GetProperty(name);
            if (field is not null)
                return 0;
            if (property is not null)
                return 1;
            return -1;
        }

        /// <summary>
        /// Gets the value at the field or property of the given name
        /// </summary>
        /// <param name="name"></param>
        /// <returns>the value</returns>
        /// <exception cref="FieldNotFoundException"></exception>
        public object? GetValueFrom(string name) => GetMember(name).GetValue(ref obj);

        /// <summary>
        /// Gets the value of the field of the given name
        /// </summary>
        /// <param name="name"></param>
        /// <returns>the value</returns>
        /// <exception cref="Exception"></exception>
        /// <exception cref="FieldNotFoundException"></exception>
        public unsafe object? GetFieldValue(string name)
        {
            FieldInfo field = GetField(name) ?? throw new FieldNotFoundException($"field with name '{name}' does not exist");
            if (field.IsStatic)
                return field.GetValue(null);
            if (obj is null)
                throw new Exception("Helper was created for type only");

            if (ObjectType.IsByRef)
                return field.GetValue(obj);
            object? valueTypeVal = field.GetValueDirect(__makeref(obj));
            StructReference valueRef = new(&valueTypeVal);
            return valueRef;
        }

        public object? GetPropertyValue(string name)
        {
            PropertyInfo property = GetProperty(name) ?? throw new FieldNotFoundException($"property with name '{name}' does not exist");
            if (property.GetMethod.IsStatic)
                return property.GetValue(null);
            if (obj is null)
                throw new Exception("Helper was created for type only");

            if (ObjectType.IsByRef)
                return property.GetValue(obj);
            return property.GetValue(obj);
        }

        /// <summary>
        /// Sets the value at the field or property of the given name
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <exception cref="FieldNotFoundException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public void SetValue(string name, dynamic value)
        {
            try
            {
                SetFieldValue(name, value);
                return;
            }
            catch { }
            try
            {
                SetPropertyValue(name, value);
            }
            catch (FieldNotFoundException)
            {
                throw new FieldNotFoundException($"field or property with name '{name}' does not exist");
            }
        }

        public void SetPropertyValue<T>(string name, T value)
        {
            PropertyInfo property = GetProperty(name) ?? throw new FieldNotFoundException($"property with name '{name}' does not exist");

            if (property.GetMethod.IsStatic)
                property.SetValue(null, value);
            else if (obj is null)
                throw new Exception("Helper was created for type only");

            // Check if the property type or the value type has a compatible implicit conversion operator
            MethodInfo? conversionMethod = TypeWorker.FindImplicitConversionMethod(property.PropertyType, typeof(T));

            object actualValue = value;

            if (conversionMethod != null)
            {
                // Convert the value using the implicit operator if it exists
                actualValue = conversionMethod.Invoke(null, new object[] { value })!;
            }

            property.SetValue(obj, actualValue);
        }

        public void SetFieldValue<T>(string name, T value)
        {
            FieldInfo field = GetField(name) ?? throw new FieldNotFoundException($"field with name '{name}' does not exist");

            if (field.IsStatic)
            {
                field.SetValue(null, value);
            }
            else if (obj is null)
            {
                throw new Exception("Helper was created for type only");
            }

            // Check if the field's type or the value type has a compatible implicit conversion operator
            MethodInfo? conversionMethod = TypeWorker.FindImplicitConversionMethod(field.FieldType, typeof(T));

            object? actualValue = value;

            if (conversionMethod != null)
            {
                // Convert the value using the implicit operator if it exists
                actualValue = conversionMethod.Invoke(null, [value]);
            }

            if (ObjectType.IsByRef)
            {
                field.SetValue(obj, actualValue);
            }
            else
            {
                field.SetValueDirect(__makeref(obj), actualValue);
            }
        }

        /// <summary>
        /// Gets the type of the field or property of the given name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        /// <exception cref="FieldNotFoundException"></exception>
        public Type GetTypeOf(string name)
        {
            int res = GetFieldOrProperty(name, out var field, out var property);
            if (res is -1)
                throw new FieldNotFoundException($"field or property with name '{name}' does not exist");
            if (res is 0)
                return field.FieldType;
            if (res is 1)
                return property.PropertyType;
            return null;
        }

        /// <summary>
        /// Invokes a method with the given name and arguments. Returns the value of the method
        /// </summary>
        /// <typeparam name="TMethodReturnType"></typeparam>
        /// <param name="methodName"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        /// <exception cref="MethodNotFoundException"></exception>
        /// <exception cref="InvalidMethodReturnTypeException"></exception>
        public TMethodReturnType? InvokeMethod<TMethodReturnType>(string methodName, params object[] args)
        {
            MethodInfo info = ObjectType.GetMethod(methodName, flags);
            if (info is null)
                throw new MethodNotFoundException($"method with name '{methodName}' does not exist");

            if(info.ReturnType != typeof(TMethodReturnType))
                throw new InvalidMethodReturnTypeException($"method with name '{methodName}' does not return a value of type '{typeof(TMethodReturnType).Name}'");

            object? res = info.Invoke(obj, args);

            return (TMethodReturnType?)res;
        }

        /// <summary>
        /// Invokes a method with the given name and arguments, does not return the value
        /// </summary>
        /// <param name="methodName"></param>
        /// <param name="args"></param>
        /// <exception cref="MethodNotFoundException"></exception>
        public void InvokeMethod(string methodName, params object[] args)
        {
            MethodInfo info = ObjectType.GetMethod(methodName, flags);
            if (info is null)
                throw new MethodNotFoundException($"method with name '{methodName}' does not exist");

            _ = info.Invoke(obj, args);
        }
    }
}

