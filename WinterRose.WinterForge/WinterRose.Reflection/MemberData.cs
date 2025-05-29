using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.ObjectiveC;
using System.Text;
using System.Threading.Tasks;

namespace WinterRose.Reflection
{
    [DebuggerDisplay("{ToDebuggerString()}")]
    public sealed class MemberData
    {
        private static readonly Dictionary<MemberInfo, MemberData> CACHE = new();

        FieldInfo? fieldsource;
        PropertyInfo? propertysource;

        public static MemberData FromField(FieldInfo field)
        {
            if (CACHE.TryGetValue(field, out var cached))
                return cached;

            var data = new MemberData
            {
                fieldsource = field,
                Attributes = field.GetCustomAttributes().ToArray()
            };

            CACHE[field] = data;
            return data;
        }

        public static MemberData FromProperty(PropertyInfo property)
        {
            if (CACHE.TryGetValue(property, out var cached))
                return cached;

            var data = new MemberData
            {
                propertysource = property,
                Attributes = property.GetCustomAttributes().ToArray()
            };

            CACHE[property] = data;
            return data;
        }

        private MemberData() { }

        public static implicit operator MemberData(FieldInfo field) => FromField(field);
        public static implicit operator MemberData(PropertyInfo property) => FromProperty(property);

        /// <summary>
        /// The identifier of the field or property.
        /// </summary>
        public string Name => fieldsource?.Name ?? propertysource?.Name ?? throw new InvalidOperationException("No field or property found.");
        /// <summary>
        /// The kind of member this is.
        /// </summary>
        public MemberTypes MemberType => fieldsource is not null ? MemberTypes.Field : MemberTypes.Property;
        /// <summary>
        /// The type of the field or property.
        /// </summary>
        public Type Type => fieldsource?.FieldType ?? propertysource?.PropertyType ?? throw new InvalidOperationException("No field or property found.");
        /// <summary>
        /// The custom attributes on the field or property.
        /// </summary>
        public Attribute[] Attributes { get; private set; }
        /// <summary>
        /// Field attributes, if this is a field. Otherwise, throws an <see cref="InvalidOperationException"/>.
        /// </summary>
        public FieldAttributes FieldAttributes => fieldsource?.Attributes ?? throw new InvalidOperationException("No field or property found.");
        /// <summary>
        /// Property attributes, if this is a property. Otherwise, throws an <see cref="InvalidOperationException"/>.
        /// </summary>
        public PropertyAttributes PropertyAttributes => propertysource?.Attributes ?? throw new InvalidOperationException("No field or property found.");
        /// <summary>
        /// Indicates if the field or property is public.
        /// </summary>
        public bool IsPublic
        {
            get
            {
                if (fieldsource != null)
                    return fieldsource.IsPublic;

                if (propertysource != null)
                {
                    MethodInfo? getMethod = propertysource.GetMethod;
                    if (getMethod != null)
                        return getMethod.IsPublic;
                }

                throw new InvalidOperationException("No field or property found.");
            }
        }
        /// <summary>
        /// Indicates if the property has a setter.
        /// </summary>
        public bool PropertyHasSetter => propertysource?.GetSetMethod(true) != null;
        /// <summary>
        /// Indicates if the field or property is static.
        /// </summary>
        public bool IsStatic
        {
            get
            {
                if (fieldsource != null)
                {
                    // Check if field is static
                    return fieldsource.IsStatic;
                }

                if (propertysource != null)
                {
                    // Check if the property getter method is static
                    var getMethod = propertysource.GetGetMethod(true);
                    if (getMethod != null)
                    {
                        return getMethod.IsStatic;
                    }
                }

                // If neither field nor property found, throw an exception
                throw new InvalidOperationException("No field or property found.");
            }
        }

        /// <summary>
        /// Indicates if the field is readonly. eg const or readonly
        /// </summary>
        public bool IsInitOnly => fieldsource?.IsInitOnly ?? throw new InvalidOperationException("No field or property found.");
        /// <summary>
        /// Indicates if the field is a literal. eg const or static readonly
        /// </summary>
        public bool IsLiteral => fieldsource?.IsLiteral ?? throw new InvalidOperationException("No field or property found.");
        /// <summary>
        /// Indicates if the field or property can be written to.
        /// </summary>
        public bool CanWrite
        {
            get
            {
                if (fieldsource is not null)
                {
                    return !fieldsource.IsInitOnly && !fieldsource.IsLiteral;
                }
                else if (propertysource is not null)
                {
                    return propertysource.CanWrite;
                }
                else
                {
                    throw new InvalidOperationException("No field or property found.");
                }
            }
        }
        /// <summary>
        /// Whether or not the type is a reference type
        /// </summary>
        public bool ByRef
        {
            get
            {
                if (fieldsource != null)
                {
                    var type = fieldsource.FieldType;
                    return type.IsByRef || type.IsValueType && !type.IsPrimitive && !type.IsEnum;
                }
                else if (propertysource != null)
                {
                    var type = propertysource.PropertyType;
                    return type.IsByRef || (type.IsValueType && !type.IsPrimitive && !type.IsEnum);
                }
                else
                {
                    throw new InvalidOperationException("No field or property found.");
                }
            }
        }


        /// <summary>
        /// Whether or not there actually is a field or property to read/write to.
        /// </summary>
        public bool IsValid => fieldsource != null || propertysource != null;

        /// <summary>
        /// Gets the value stored at this field or property
        /// </summary>
        /// <returns>The object stored in the field or property</returns>
        /// <exception cref="InvalidOperationException"></exception>
        public unsafe object? GetValue(ref object? obj)
        {
            if (propertysource is null && fieldsource is null)
                throw new InvalidOperationException("No property or field found.");

            if (obj.GetType().IsValueType)
            {
                TypedReference tr = __makeref(obj);
                object? valueTypeVal = fieldsource.GetValueDirect(tr);
                return valueTypeVal;
            }

            if (fieldsource is not null)
                return fieldsource.GetValue(obj);
            return propertysource!.GetValue(obj);
        }

        /// <summary>
        /// For a static value.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public unsafe object? GetValue()
        {
            if (propertysource is null && fieldsource is null)
                throw new InvalidOperationException("No property or field found.");

            if (fieldsource is not null)
                return fieldsource.GetValue(null);
            return propertysource!.GetValue(null);
        }
        /// <summary>
        /// Writes the value to the field or property. If the field or property is readonly, an <see cref="InvalidOperationException"/> is thrown.
        /// </summary>
        /// <param name="value"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public void SetValue(ref object? obj, object? value)
        {
            if (fieldsource is not null)
                SetFieldValue(ref obj, value);
            else if (propertysource is not null)
                SetPropertyValue(ref obj, value);
            else
                throw new Exception("Field or property does not exist with name: " + Name);
        }

        public void SetPropertyValue<T>(ref object? obj, T value)
        {
            object actualValue = value;
            if (value != null)
            {
                if (TypeWorker.SupportedPrimitives.Contains(Type)
                    && TypeWorker.SupportedPrimitives.Contains(value.GetType())
                    && Type != value.GetType())
                    actualValue = TypeWorker.CastPrimitive(value, Type);
                else if (TypeWorker.FindImplicitConversionMethod(Type, value.GetType()) is MethodInfo conversionMethod)
                    actualValue = conversionMethod.Invoke(null, [value])!;
            }

            if (obj is null && !propertysource.SetMethod.IsStatic && !(Type.IsAbstract && Type.IsSealed))
                throw new Exception("Reflection helper was created type only.");

            propertysource.SetValue(obj, actualValue);
        }

        public void SetFieldValue<T>(ref object obj, T value)
        {
            object actualValue = value;
            if (value != null)
            {
                if (TypeWorker.SupportedPrimitives.Contains(Type) 
                    && TypeWorker.SupportedPrimitives.Contains(value.GetType())
                    && Type != value.GetType())
                    actualValue = TypeWorker.CastPrimitive(value, Type);
                else if (TypeWorker.FindImplicitConversionMethod(Type, value.GetType()) is MethodInfo conversionMethod)
                        actualValue = conversionMethod.Invoke(null, [value])!;
            }

            if (obj is null && !fieldsource.IsStatic && !(Type.IsAbstract && Type.IsSealed))
                throw new Exception("Reflection helper was created type only.");

            if (!obj.GetType().IsValueType)
                fieldsource.SetValue(obj, actualValue);
            else
                fieldsource!.SetValueDirect(__makeref(obj), actualValue);
        }

        /// <summary>
        /// Gets whether the field or property has the provided attribute
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>True if the field or property has at least 1 attribute of the given type <typeparamref name="T"/></returns>
        public bool HasAttribute<T>()
        {
            foreach (var attr in Attributes)
            {
                if (attr is T)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Gets the attribute of the specified type <typeparamref name="T"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>The first found attribute of type <typeparamref name="T"/>. if there is no such attribute, <c>null</c> is returned</returns>
        public T? GetAttribute<T>() where T : Attribute
        {
            foreach (var attr in Attributes)
            {
                if (attr is T a)
                    return a;
            }
            return null;
        }

        private string ToDebuggerString()
        {
            string publicOrPrivate = IsPublic ? "Public" : "Private";
            string writable = CanWrite ? "Writable" : "Readonly";
            string propOrField = MemberType == MemberTypes.Field ? "Field" : "Property";
            return $"{publicOrPrivate} {propOrField} <{{{Name}}} = {writable}";
        }

        public static implicit operator FieldInfo(MemberData d) => d.fieldsource ?? throw new InvalidCastException("Member was not a field");
        public static implicit operator PropertyInfo(MemberData d) => d.propertysource ?? throw new InvalidCastException("Member was not a property");
    }
}
