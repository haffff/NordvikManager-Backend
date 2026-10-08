#nullable enable
using System;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    /// <summary>
    /// The "Type" argument of Set Variable / Set Detail: the short names shown in their help
    /// ('int', 'bool', ...). A full .NET type name (e.g. 'System.Int32') still works; anything
    /// else is treated as string.
    /// </summary>
    internal static class ActionValueTypes
    {
        public static Type Resolve(string? typeName) => typeName?.Trim().ToLowerInvariant() switch
        {
            null or "" or "string" => typeof(string),
            "int" or "int32" => typeof(int),
            "long" or "int64" => typeof(long),
            "float" or "single" => typeof(float),
            "double" => typeof(double),
            "bool" or "boolean" => typeof(bool),
            _ => Type.GetType(typeName.Trim()) ?? typeof(string),
        };
    }
}
