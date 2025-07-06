//using System;
//using System.Collections.Generic;
//using System.Dynamic;
//using System.Linq;
//using System.Reflection;
//using System.Text;
//using System.Threading.Tasks;

//namespace WinterRose.Reflection
//{
//    /// <summary>
//    /// Provides helper functions
//    /// </summary>
//    public class ReflectionHelper<T> : DynamicObject
//    {
//        public static ReflectionHelper<T> ForObject(ref T o) => new(ref o);

//        public static ReflectionHelper<T> ForType(Type type) => new(type);

//        private static MemberDataCollection<T> cachedMembers;

//        private readonly static BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
//        T obj;
//        /// <summary>
//        /// The object on which the helper was created
//        /// </summary>
//        public T Value => obj;
//        private object nullobj = null!;
//        /// <summary>
//        /// The type of the object on which the helper was created
//        /// </summary>
//        public Type ObjectType { get; init; }

//        public ReflectionHelper(ref T obj)
//        {
//            this.obj = obj;
//            ObjectType = obj.GetType();
//            cachedMembers ??= new(this);
//        }
//        public ReflectionHelper(Type objType)
//        {
//            ObjectType = objType;
//            cachedMembers ??= new(this); 
//        }

//        /// <summary>
//        /// Gets the <see cref="MemberData"/> represenation of the field or property of the given name
//        /// </summary>
//        /// <param name="name"></param>
//        /// <returns></returns>
//        /// <exception cref="FieldNotFoundException"></exception>
//        public MemberData GetMember(string name)
//        {
//            if (cachedMembers.TryGet(name, out MemberData? memer))
//                return memer!;

//            int res = GetFieldOrProperty(name, out var field, out var property);
//            if (res is -1)
//                throw new FieldNotFoundException($"field or property with name '{name}' does not exist");
//            if (res is 0)
//                return field!;
//            if (res is 1)
//                return property!;

//            return null!;
//        }

//        /// <summary>
//        /// Gets all the fields and properties of the object
//        /// </summary>
//        /// <returns></returns>
//        public MemberDataCollection<T> GetMembers()
//        {
//            if(cachedMembers is not null)
//                return cachedMembers;
//            var members = CreateMemberList();
//            return new(members);
//        }

//        public MethodInfo GetMethod(string method) => ObjectType.GetMethod(method, flags);

//        private List<MemberData> CreateMemberList()
//        {
//            List<MemberData> members = [.. ObjectType.GetFields(flags), .. ObjectType.GetProperties(flags)];
//            return members;
//        }

//        public FieldInfo? GetField(string name) => ObjectType.GetField(name, flags);
//        public PropertyInfo? GetProperty(string name) => ObjectType.GetProperty(name, flags);

//        public override bool TryGetMember(GetMemberBinder binder, out object? result)
//        {
//            MemberData? member;
//            cachedMembers.TryGet(binder.Name, out member);

//            if (member is null)
//            {
//                try
//                {
//                    member = GetMember(binder.Name);
//                }
//                catch
//                {
//                    result = null;
//                    return false;
//                }
//            }

//            result = member;
//            return true;
//        }

//        public override bool TrySetMember(SetMemberBinder binder, object? value)
//        {
//            SetValue(binder.Name, value);
//            return true;
//        }

//        /// <summary>
//        /// Gets a field or a property with the given name
//        /// </summary>
//        /// <param name="name"></param>
//        /// <param name="field"></param>
//        /// <param name="property"></param>
//        /// <returns>0 if a field was found, 1 if a property was found, and -1 if neither was found</returns>
//        public int GetFieldOrProperty(string name, out FieldInfo? field, out PropertyInfo? property)
//        {
//            field = GetField(name);
//            property = GetProperty(name);
//            if (field is not null)
//                return 0;
//            if (property is not null)
//                return 1;
//            return -1;
//        }

//        /// <summary>
//        /// Gets the value at the field or property of the given name
//        /// </summary>
//        /// <param name="name"></param>
//        /// <returns>the value</returns>
//        /// <exception cref="FieldNotFoundException"></exception>
//        public object? GetValueFrom(string name)
//        {
//            if (cachedMembers.TryGet(name, out MemberData memer))
//                return memer!.GetValue(ref obj);

//            int res = GetFieldOrProperty(name, out var field, out var property);
//            if (res is -1)
//                throw new FieldNotFoundException($"field or property with name '{name}' does not exist");
//            if (res is 0)
//                return field!.GetValue(obj);
//            if (res is 1)
//                return property!.GetValue(obj);
//            return null;
//        }

//        /// <summary>
//        /// Sets the value at the field or property of the given name
//        /// </summary>
//        /// <param name="name"></param>
//        /// <param name="value"></param>
//        /// <exception cref="FieldNotFoundException"></exception>
//        /// <exception cref="InvalidOperationException"></exception>
//        public void SetValue(string name, dynamic value)
//        {
//            try
//            {
//                SetFieldValue(name, value);
//                return;
//            }
//            catch { }
//            try
//            {
//                SetPropertyValue(name, value);
//            }
//            catch (FieldNotFoundException)
//            {
//                throw new FieldNotFoundException($"field or property with name '{name}' does not exist");
//            }
//        }

//        public void SetPropertyValue<T>(string name, T value)
//        {
//            PropertyInfo property = GetProperty(name) ?? throw new FieldNotFoundException($"property with name '{name}' does not exist");

//            if (property.GetMethod.IsStatic)
//                property.SetValue(null, value);
//            else if (obj is null)
//                throw new Exception("Helper was created for type only");

//            // Check if the property type or the value type has a compatible implicit conversion operator
//            MethodInfo? conversionMethod = TypeWorker.FindImplicitConversionMethod(property.PropertyType, typeof(T));

//            object actualValue = value;

//            if (conversionMethod != null)
//            {
//                // Convert the value using the implicit operator if it exists
//                actualValue = conversionMethod.Invoke(null, new object[] { value })!;
//            }

//            property.SetValue(obj, actualValue);
//        }

//        public void SetFieldValue<T>(string name, T value)
//        {
//            FieldInfo field = GetField(name) ?? throw new FieldNotFoundException($"field with name '{name}' does not exist");

//            if (field.IsStatic)
//            {
//                field.SetValue(null, value);
//            }
//            else if (obj is null)
//            {
//                throw new Exception("Helper was created for type only");
//            }

//            // Check if the field's type or the value type has a compatible implicit conversion operator
//            MethodInfo? conversionMethod = TypeWorker.FindImplicitConversionMethod(field.FieldType, typeof(T));

//            object? actualValue = value;

//            if (conversionMethod != null)
//            {
//                // Convert the value using the implicit operator if it exists
//                actualValue = conversionMethod.Invoke(null, [value]);
//            }

//            if (ObjectType.IsByRef)
//            {
//                field.SetValue(obj, actualValue);
//            }
//            else
//            {
//                field.SetValueDirect(__makeref(obj), actualValue);
//            }
//        }

//        /// <summary>
//        /// Gets the type of the field or property of the given name
//        /// </summary>
//        /// <param name="name"></param>
//        /// <returns></returns>
//        /// <exception cref="FieldNotFoundException"></exception>
//        public Type GetTypeOf(string name)
//        {
//            if (cachedMembers.TryGet(name, out MemberData memer))
//                return memer.Type;

//            int res = GetFieldOrProperty(name, out var field, out var property);
//            if (res is -1)
//                throw new FieldNotFoundException($"field or property with name '{name}' does not exist");
//            if (res is 0)
//                return field.FieldType;
//            if (res is 1)
//                return property.PropertyType;
//            return null;
//        }

//    }
//}
