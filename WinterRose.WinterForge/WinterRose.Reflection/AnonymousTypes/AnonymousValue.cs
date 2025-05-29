using System;
using System.Collections.Generic;

namespace WinterRose.AnonymousTypes;

/// <summary>
/// A struct that represents a value in an object. This is mainly used to represent values in an anonymous object. but through the AnonymousObjectReader it can be used to represent any class or struct its fields or properties.
/// </summary>
public readonly struct AnonymousValue
{
    /// <summary>
    /// The name of the field or property
    /// </summary>
    public readonly string Name { get; }

    /// <summary>
    /// The value of the field or property
    /// </summary>
    public readonly object? Value { get; }

    /// <summary>
    /// The type of the value of the field or property. Null if the value is null.
    /// </summary>
    public readonly Type? ValueType => Value?.GetType();

    /// <summary>
    /// The value of the field or property as a <see cref="bool"/>
    /// </summary>
    public readonly bool BoolValue => (bool)Value;
    /// <summary>
    /// The value of the field or property as a <see cref="byte"/>
    /// </summary>
    public readonly byte ByteValue => (byte)Value;
    /// <summary>
    /// The value of the field or property as a <see cref="sbyte"/>
    /// </summary>
    public readonly sbyte SByteValue => (sbyte)Value;
    /// <summary>
    /// The value of the field or property as a <see cref="short"/>
    /// </summary>
    public readonly short ShortValue => (short)Value;
    /// <summary>
    /// The value of the field or property as a <see cref="ushort"/>
    /// </summary>
    public readonly ushort UShortValue => (ushort)Value;
    /// <summary>
    /// The value of the field or property as a <see cref="int"/>
    /// </summary>
    public readonly int IntValue => (int)Value;
    /// <summary>
    /// The value of the field or property as a <see cref="uint"/>
    /// </summary>
    public readonly uint UIntValue => (uint)Value;
    /// <summary>
    /// The value of the field or property as a <see cref="long"/>
    /// </summary>
    public readonly long LongValue => (long)Value;
    /// <summary>
    /// The value of the field or property as a <see cref="ulong"/>
    /// </summary>
    public readonly ulong ULongValue => (ulong)Value;
    /// <summary>
    /// The value of the field or property as a <see cref="float"/>
    /// </summary>
    public readonly float FloatValue => (float)Value;
    /// <summary>
    /// The value of the field or property as a <see cref="double"/>
    /// </summary>
    public readonly double DoubleValue => (double)Value;
    /// <summary>
    /// The value of the field or property as a <see cref="decimal"/>
    /// </summary>
    public readonly decimal DecimalValue => (decimal)Value;
    /// <summary>
    /// The value of the field or property as a <see cref="char"/>
    /// </summary>
    public readonly char CharValue => (char)Value;
    /// <summary>
    /// The value of the field or property as a <see cref="string"/> <br></br>
    /// 
    /// If the value is not a string, it will call <see cref="object.ToString"/> on the value.
    /// </summary>
    public readonly string? StringValue => Value?.ToString();
    /// <summary>
    /// The value if it is a <see cref="AnonymousTypeReader"/> <br></br>
    /// 
    /// Allows for accessing deeper values in the object.
    /// </summary>
    public readonly AnonymousTypeReader AnonymousObjectReaderValue => Value as AnonymousTypeReader;

    public readonly Anonymous CompiledAnonymousValue => Value as Anonymous;

    /// <summary>
    /// The value if it is a <see cref="Delegate"/> (method) <br></br>
    /// 
    /// This method may return a value, and have parameters. This is unknown by the IDE at write time.
    /// </summary>
    public readonly AnonymousMethod? MethodValue => Value as AnonymousMethod;

    /// <summary>
    /// Whether the value is null or not.
    /// </summary>
    public readonly bool HasValue => Value != null;

    /// <summary>
    /// Creates a new instance of <see cref="AnonymousValue"/>
    /// </summary>
    /// <param name="name"></param>
    /// <param name="value"></param>
    public AnonymousValue(string name, object? value)
    {
        Name = name;
        Value = value;
    }

    /// <summary>
    /// Allows for accessing the value if it is a <see cref="AnonymousTypeReader"/> and the value is not null.
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public AnonymousValue this[string name]
    {
        get
        {
            if (Value is AnonymousTypeReader reader)
                return reader[name];
            return new("", null);
        }
    }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public static implicit operator AnonymousValue(KeyValuePair<string, object> pair) => new AnonymousValue(pair.Key, pair.Value);
    public static implicit operator KeyValuePair<string, object?>(AnonymousValue value) => new KeyValuePair<string, object?>(value.Name, value.Value);
    public static implicit operator bool(AnonymousValue value) => value.BoolValue;
    public static implicit operator byte(AnonymousValue value) => value.ByteValue;
    public static implicit operator sbyte(AnonymousValue value) => value.SByteValue;
    public static implicit operator short(AnonymousValue value) => value.ShortValue;
    public static implicit operator ushort(AnonymousValue value) => value.UShortValue;
    public static implicit operator int(AnonymousValue value) => value.IntValue;
    public static implicit operator uint(AnonymousValue value) => value.UIntValue;
    public static implicit operator long(AnonymousValue value) => value.LongValue;
    public static implicit operator ulong(AnonymousValue value) => value.ULongValue;
    public static implicit operator float(AnonymousValue value) => value.FloatValue;
    public static implicit operator double(AnonymousValue value) => value.DoubleValue;
    public static implicit operator decimal(AnonymousValue value) => value.DecimalValue;
    public static implicit operator char(AnonymousValue value) => value.CharValue;
    public static implicit operator string(AnonymousValue value) => value.StringValue;
    public static implicit operator AnonymousTypeReader(AnonymousValue value) => value.AnonymousObjectReaderValue;
    public static implicit operator AnonymousMethod(AnonymousValue value) => value.MethodValue ?? throw new InvalidCastException("Value is not an AnonymousMethod");
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
