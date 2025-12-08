using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.ObjectiveC;
using System.Text;
using System.Threading.Tasks;

namespace WinterRose.Reflection
{
    /// <summary>
    /// Represents a field or property under a unified API
    /// </summary>
    [DebuggerDisplay("{ToDebuggerString()}")]
    public class MemberData
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

        public static MemberData FromMemberInfo(MemberInfo member)
        {
            if (member is FieldInfo field)
                return FromField(field);
            else if (member is PropertyInfo property)
                return FromProperty(property);
            throw new InvalidOperationException("Only fields and properties are supported");
        }

        protected MemberData()
        {
        }

        public static implicit operator MemberData(FieldInfo field) => FromField(field);
        public static implicit operator MemberData(PropertyInfo property) => FromProperty(property);

        /// <summary>
        /// The identifier of the field or property.
        /// </summary>
        public virtual string Name => fieldsource?.Name ??
                                      propertysource?.Name ??
                                      throw new InvalidOperationException("No field or property found.");

        /// <summary>
        /// The kind of member this is.
        /// </summary>
        public virtual MemberTypes MemberType => fieldsource is not null ? MemberTypes.Field : MemberTypes.Property;

        /// <summary>
        /// The type of the field or property.
        /// </summary>
        public virtual Type Type => fieldsource?.FieldType ?? propertysource?.PropertyType ??
            throw new InvalidOperationException("No field or property found.");

        /// <summary>
        /// The custom attributes on the field or property.
        /// </summary>
        public Attribute[] Attributes { get; private set; }

        /// <summary>
        /// Field attributes, if this is a field. Otherwise, throws an <see cref="InvalidOperationException"/>.
        /// </summary>
        public virtual FieldAttributes FieldAttributes => fieldsource?.Attributes ??
                                                          throw new InvalidOperationException(
                                                              "No field or property found.");

        /// <summary>
        /// Property attributes, if this is a property. Otherwise, throws an <see cref="InvalidOperationException"/>.
        /// </summary>
        public virtual PropertyAttributes PropertyAttributes => propertysource?.Attributes ??
                                                                throw new InvalidOperationException(
                                                                    "No field or property found.");

        /// <summary>
        /// Indicates if the field or property is public.
        /// </summary>
        public virtual bool IsPublic
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
        public virtual bool PropertyHasSetter => propertysource?.GetSetMethod(true) != null;

        /// <summary>
        /// Indicates if the field or property is static.
        /// </summary>
        public virtual bool IsStatic
        {
            get
            {
                if (fieldsource != null)
                {
                    return fieldsource.IsStatic;
                }

                if (propertysource != null)
                {
                    var getMethod = propertysource.GetGetMethod(true);
                    if (getMethod != null)
                    {
                        return getMethod.IsStatic;
                    }
                }

                throw new InvalidOperationException("This MemberData object is invalid");
            }
        }

        /// <summary>
        /// Indicates if the field is readonly. eg const or readonly
        /// </summary>
        public virtual bool IsInitOnly => fieldsource?.IsInitOnly ??
                                          throw new InvalidOperationException(
                                              "This Memberdata object represents a property");

        /// <summary>
        /// Indicates if the field is a literal. eg const or static readonly
        /// </summary>
        public virtual bool IsLiteral => fieldsource?.IsLiteral ??
                                         throw new InvalidOperationException(
                                             "This MemberData object represents a property.");

        /// <summary>
        /// Indicates if the field or property can be written to.
        /// </summary>
        public virtual bool CanWrite
        {
            get
            {
                if (HasAttribute<ReadOnlyAttribute>())
                    return false;
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
                    throw new InvalidOperationException("This MemberData object is invalid");
                }
            }
        }

        /// <summary>
        /// Whether or not the type is a reference type
        /// </summary>
        public virtual bool ByRef
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
                    throw new InvalidOperationException("This MemberData object is invalid");
                }
            }
        }


        /// <summary>
        /// Whether or not there actually is a field or property to read/write to.
        /// </summary>
        public virtual bool IsValid
        {
            get
            {
                if (fieldsource is not null)
                    return true;
                if (propertysource is null)
                    return false;

                if (propertysource.GetIndexParameters().Length != 0)
                    return false;
                return true;
            }
        }

        /// <summary>
        /// The type that declares the member
        /// </summary>
        public Type? DeclaringType
        {
            get
            {
                if (fieldsource is not null)
                    return fieldsource.DeclaringType;
                if (propertysource is not null)
                    return propertysource.DeclaringType;
                throw new InvalidOperationException("This MemberData object is invalid");
            }
        }

        /// <summary>
        /// Gets the value stored at this field or property
        /// </summary>
        /// <returns>The object stored in the field or property</returns>
        /// <exception cref="InvalidOperationException"></exception>
        public object? GetValue<TTarget>(TTarget? obj) => GetValue(ref obj);

        /// <summary>
        /// Gets the value stored at this field or property
        /// </summary>
        /// <returns>The object stored in the field or property</returns>
        /// <exception cref="InvalidOperationException"></exception>
        public virtual object? GetValue<TTarget>(ref TTarget? obj)
        {
            if (propertysource is null && fieldsource is null)
                throw new InvalidOperationException("This MemberData object is invalid");

            if (obj.GetType().IsValueType && fieldsource is not null)
            {
                TypedReference tr = __makeref(obj);
                object? valueTypeVal = fieldsource.GetValueDirect(tr);
                return valueTypeVal;
            }

            if (fieldsource is not null)
                return fieldsource.GetValue(obj);
            return propertysource!.GetValue(obj);
        }

        public delegate ref TField FieldRefGetter<TTarget, TField>(TTarget target);

        public delegate ref TField FieldRefGetterByRef<TTarget, TField>(ref TTarget target);

        /// <summary>
        /// Returns a by-ref reference to a field on the provided target object.
        /// Only fields are supported (not properties). Uses DynamicMethod + IL generation.
        /// </summary>
        /// <typeparam name="TTarget">Type of the target object (owner of the field).</typeparam>
        /// <typeparam name="TField">Type of the field to return by ref.</typeparam>
        /// <param name="obj">Target instance (passed by ref for value-type owners).</param>
        /// <returns>Reference to the field value.</returns>
        public ref TField GetValueRef<TTarget, TField>(ref TTarget obj)
        {
            if (propertysource is not null)
                throw new InvalidOperationException("Cannot return a ref for a property; only fields are supported.");

            if (fieldsource is null)
                throw new InvalidOperationException("This MemberData object is invalid (no field info).");

            var field = fieldsource; // FieldInfo
            var fieldType = field.FieldType;
            var wantedFieldType = typeof(TField);
            if (fieldType != wantedFieldType)
                throw new InvalidOperationException(
                    $"Field type mismatch. Field is '{fieldType}', requested '{wantedFieldType}'.");

            Type ownerParamType = typeof(TTarget); // used for the dynamic-method parameter types (must match delegate)
            Type ownerForAccess = field.DeclaringType ?? typeof(TTarget); // used as 'owner' for DynamicMethod ctor

            // Static field: produce a dynamic method with no owner parameter
            if (field.IsStatic)
            {
                if (field.IsInitOnly)
                    throw new InvalidOperationException("Cannot return a ref to an init-only (readonly) static field.");

                var dm = new DynamicMethod(
                    $"__get_stc_field_ref_{ownerForAccess.Name}_{field.Name}",
                    wantedFieldType.MakeByRefType(),
                    Type.EmptyTypes,
                    ownerForAccess,
                    true);

                var il = dm.GetILGenerator();
                il.Emit(OpCodes.Ldsflda, field); // load static field address
                il.Emit(OpCodes.Ret);

                var del = (FuncRefWrapper<TField>)dm.CreateDelegate(typeof(FuncRefWrapper<TField>));
                // helper delegate wrapper to call the no-arg dynamic method and get ref
                return ref del.Invoke();
            }

            // Instance field: two cases: owner is value-type or reference-type
            if (ownerParamType.IsValueType)
            {
                // DM signature: ref TField Method(ref TTarget owner)
                var dm = new DynamicMethod(
                    $"__get_vt_field_ref_{ownerParamType.Name}_{field.Name}",
                    wantedFieldType.MakeByRefType(),
                    new Type[] { ownerParamType.MakeByRefType() },  // must match FieldRefGetterByRef<TTarget,TField>
                    ownerForAccess,
                    true);

                var il = dm.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0); // load managed reference to value-type owner (TTarget&)
                il.Emit(OpCodes.Ldflda, field); // load field address within value type
                il.Emit(OpCodes.Ret);

                var typedDel =
                    (FieldRefGetterByRef<TTarget, TField>)dm.CreateDelegate(
                        typeof(FieldRefGetterByRef<TTarget, TField>));
                if (obj is null)
                    throw new NullReferenceException("Target is null.");
                // call and return the ref
                return ref typedDel(ref obj!);
            }
            else
            {
                // owner is reference type
                // DM signature: ref TField Method(TTarget owner)
                var dm = new DynamicMethod(
                    $"__get_ref_field_ref_{ownerParamType.Name}_{field.Name}",
                    wantedFieldType.MakeByRefType(),
                    new Type[] { ownerParamType }, // must match FieldRefGetter<TTarget,TField>
                    ownerForAccess,
                    true);

                var il = dm.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0); // push object reference
                il.Emit(OpCodes.Ldflda, field); // load address of instance field
                il.Emit(OpCodes.Ret);

                var typedDel =
                    (FieldRefGetter<TTarget, TField>)dm.CreateDelegate(typeof(FieldRefGetter<TTarget, TField>));
                if (obj is null)
                    throw new NullReferenceException("Target is null.");

                // For reference-type owners we pass the instance (not byref)
                return ref typedDel(obj!);
            }
        }

        // helper delegate type & wrapper used for static-field no-arg dynamicmethod case
        // it's necessary because a dynamic method returning ref needs a delegate type that returns ref.
        // we create a small strongly typed delegate on the fly.
        public delegate ref TField FuncRefWrapper<TField>();

        /// <summary>
        /// For a static value.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public unsafe object? GetValue()
        {
            if (propertysource is null && fieldsource is null)
                throw new InvalidOperationException("This MemberData object is invalid");

            if (fieldsource is not null)
                return fieldsource.GetValue(null);
            return propertysource!.GetValue(null);
        }

        /// <summary>
        /// Writes the value to the field or property. If the field or property is readonly, an <see cref="InvalidOperationException"/> is thrown.
        /// </summary>
        /// <param name="value"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public virtual void SetValue<TTarget, TValue>(ref TTarget? obj, TValue? value)
        {
            if (fieldsource is not null)
                SetFieldValue(ref obj, value);
            else if (propertysource is not null)
                SetPropertyValue(ref obj, value);
            else
                throw new Exception("This MemberData object is invalid");
        }

        /// <summary>
        /// Writes the value to the field or property. If the field or property is readonly, an <see cref="InvalidOperationException"/> is thrown.
        /// </summary>
        /// <param name="value"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public void SetValue<TTarget, TValue>(TTarget? obj, TValue? value)
        {
            SetValue(ref obj, value);
        }

        public virtual void SetPropertyValue<TTarget, TValue>(ref TTarget? obj, TValue value)
        {
            if (obj is null && !propertysource.SetMethod.IsStatic && !(Type.IsAbstract && Type.IsSealed))
                throw new Exception("Reflection helper was created type only.");

            object actualValue = value;
            if (Type.IsEnum)
            {
                if (value.GetType() != Type)
                {
                    if (value.GetType() != Enum.GetUnderlyingType(Type))
                    {
                        actualValue = CastValue(value, Enum.GetUnderlyingType(Type));
                    }

                    actualValue = Enum.ToObject(Type, actualValue);
                }
            }
            else if (value != null && value.GetType() != Type)
            {
                actualValue = CastValue(value, Type);
            }

            propertysource.SetValue(obj, actualValue);
        }

        private static object CastValue<T>(T value, Type target)
        {
            if (target == typeof(object))
                return value;
            if (TypeWorker.SupportedPrimitives.Contains(target)
                && TypeWorker.SupportedPrimitives.Contains(value.GetType())
                && target != value.GetType())
                return TypeWorker.CastPrimitive(value, target);
            else if (TypeWorker.FindImplicitConversionMethod(target, value.GetType()) is MethodInfo conversionMethod)
                return conversionMethod.Invoke(null, [value])!;
            else if (TypeConverter.CanConvert(value.GetType(), target))
                return TypeConverter.Convert(value, target);
            return value;
        }

        public virtual void SetFieldValue<TTarget, TValue>(ref TTarget? obj, TValue value)
        {
            if (obj is null && !fieldsource.IsStatic && !(Type.IsAbstract && Type.IsSealed))
                throw new Exception("Reflection helper was created type only.");

            object actualValue = value;
            if (Type.IsEnum)
            {
                if (value.GetType() != Type)
                {
                    if (value.GetType() != Enum.GetUnderlyingType(Type))
                    {
                        actualValue = CastValue(value, Enum.GetUnderlyingType(Type));
                    }

                    actualValue = Enum.ToObject(Type, actualValue);
                }
            }
            else if (value != null && value.GetType() != Type)
            {
                actualValue = CastValue(value, Type);
            }


            if (obj is null)
                fieldsource.SetValue(null, actualValue);
            else if (!obj.GetType().IsValueType)
                fieldsource.SetValue(obj, actualValue);
            else
                fieldsource!.SetValueDirect(__makeref(obj), actualValue);
        }

        /// <summary>
        /// Gets whether the field or property has the provided attribute
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>True if the field or property has at least 1 attribute of the given type <typeparamref name="T"/></returns>
        public virtual bool HasAttribute<T>()
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
        public virtual T? GetAttribute<T>() where T : Attribute
        {
            foreach (var attr in Attributes)
            {
                if (attr is T a)
                    return a;
            }

            return null;
        }

        /// <summary>
        /// Gets the attribute of the specified type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>The first found attribute of type. if there is no such attribute, <c>null</c> is returned</returns>
        public virtual Attribute? GetAttribute(Type attrType)
        {
            foreach (var attr in Attributes)
            {
                if (attr.GetType().IsAssignableTo(attrType))
                    return attr;
            }

            return null;
        }

        protected virtual string ToDebuggerString()
        {
            string publicOrPrivate = IsPublic ? "Public" : "Private";
            string writable = CanWrite ? "Writable" : "Readonly";
            string propOrField = MemberType == MemberTypes.Field ? "Field" : "Property";
            return $"{publicOrPrivate} {propOrField} <{{{Name}}} = {writable}>";
        }

        public static implicit operator FieldInfo(MemberData d) =>
            d.fieldsource ?? throw new InvalidCastException("Member was not a field");

        public static implicit operator PropertyInfo(MemberData d) =>
            d.propertysource ?? throw new InvalidCastException("Member was not a property");
    }
}