using System;
using System.Runtime.Serialization;

namespace WinterRose.Reflection
{
    /// <summary>
    /// Thrown when a method has an invalid return type based on the context it is being used in
    /// </summary>
    [Serializable]
    public class InvalidMethodReturnTypeException(string? message) : Exception(message);
}