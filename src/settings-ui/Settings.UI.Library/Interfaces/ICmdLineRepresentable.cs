// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Reflection;

namespace Microsoft.PowerToys.Settings.UI.Library;

/// <summary>
/// A helper interface to allow parsing property values from their command line representation.
/// </summary>
public interface ICmdLineRepresentable
{
    public static abstract bool TryParseFromCmd(string cmd, out object result);

    public abstract bool TryToCmdRepresentable(out string result);

    public static sealed bool TryToCmdRepresentableFor(Type type, object value, out string result)
    {
        result = null;
        if (!typeof(ICmdLineRepresentable).IsAssignableFrom(type))
        {
            throw new ArgumentException($"{type} doesn't implement {nameof(ICmdLineRepresentable)}");
        }

        var method = type.GetMethod(nameof(TryToCmdRepresentable));
        var parameters = new object[] { result };
        if ((bool)method.Invoke(value, parameters))
        {
            result = (string)parameters[0];
            return true;
        }

        return false;
    }

    public static sealed bool TryParseFromCmdFor(Type type, string cmd, out object result)
    {
        result = null;
        if (!typeof(ICmdLineRepresentable).IsAssignableFrom(type))
        {
            throw new ArgumentException($"{type} doesn't implement {nameof(ICmdLineRepresentable)}");
        }

        var method = type.GetMethod(nameof(TryParseFromCmd), BindingFlags.Static | BindingFlags.Public);
        var parameters = new object[] { cmd, null };
        if ((bool)method.Invoke(null, parameters))
        {
            result = parameters[1];
            return true;
        }

        return false;
    }

    public static sealed object ParseFor(Type type, string cmdRepr)
    {
        if (type.IsEnum)
        {
            return Enum.Parse(type, cmdRepr);
        }
        else if (type.IsPrimitive)
        {
            if (type == typeof(bool))
            {
                return bool.Parse(cmdRepr.ToLowerInvariant());
            }
            else
            {
                // Converts numeric types like Uint32
                return Convert.ChangeType(cmdRepr, type, CultureInfo.InvariantCulture);
            }
        }
        else if (type.IsValueType && type == typeof(DateTimeOffset))
        {
            if (DateTimeOffset.TryParse(cmdRepr, out var structResult))
            {
                return structResult;
            }

            throw new ArgumentException($"Invalid DateTimeOffset format '{cmdRepr}'");
        }
        else if (type.IsClass)
        {
            if (type == typeof(string))
            {
                return cmdRepr;
            }
            else
            {
                TryParseFromCmdFor(type, cmdRepr, out var classResult);
                return classResult;
            }
        }

        throw new NotImplementedException($"Parsing type {type} is not supported yet");
    }

    public static string ToCmdRepr(Type type, object value)
    {
        if (type.IsEnum || type.IsPrimitive)
        {
            return value.ToString();
        }
        else if (type.IsValueType && type == typeof(DateTimeOffset))
        {
            return ((DateTimeOffset)value).ToString("o");
        }
        else if (type.IsClass)
        {
            if (type == typeof(string))
            {
                return (string)value;
            }
            else
            {
                TryToCmdRepresentableFor(type, value, out var result);
                return result;
            }
        }

        throw new NotImplementedException($"CmdRepr of {type} is not supported yet");
    }
}
