// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using PowerScripts.Core.Manifest;

namespace PowerScripts.Core.Cli;

internal static class CliParameterResolver
{
    internal static bool TryResolve(
        BuiltinCliDefinition definition,
        IReadOnlyDictionary<string, string?> supplied,
        out Dictionary<string, string?> resolved,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(supplied);
        resolved = new(StringComparer.OrdinalIgnoreCase);
        error = string.Empty;
        var declarations = definition.Parameters.ToDictionary(parameter => parameter.Name, StringComparer.OrdinalIgnoreCase);
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in supplied)
        {
            if (!declarations.ContainsKey(name))
            {
                error = $"Unknown parameter '{name}' for command '{definition.Id}'.";
                return false;
            }

            if (!values.TryAdd(name, value))
            {
                error = $"Parameter '{name}' was supplied more than once.";
                return false;
            }
        }

        foreach (var parameter in definition.Parameters)
        {
            var suppliedValue = values.TryGetValue(parameter.Name, out var value);
            value = suppliedValue ? value : parameter.Default;
            if (string.IsNullOrEmpty(value))
            {
                if (parameter.IsRequired)
                {
                    error = $"Missing required parameter '{parameter.Name}'.";
                    return false;
                }

                if (suppliedValue && (parameter.IsInt || parameter.IsBool || parameter.IsChoice))
                {
                    return TryValidate(parameter, value ?? string.Empty, out _, out error);
                }

                if (suppliedValue || parameter.Default is not null)
                {
                    resolved[parameter.Name] = value;
                }

                continue;
            }

            if (!TryValidate(parameter, value, out var normalized, out error))
            {
                return false;
            }

            resolved[parameter.Name] = normalized;
        }

        return true;
    }

    internal static bool TryValidate(ScriptParameter parameter, string value, out string normalized, out string error)
    {
        normalized = value;
        error = string.Empty;
        if (value.Contains('\0'))
        {
            error = $"Parameter '{parameter.Name}' cannot contain a NUL character.";
            return false;
        }

        if (parameter.IsChoice)
        {
            if (!parameter.Options.Contains(value, StringComparer.Ordinal))
            {
                error = $"Parameter '{parameter.Name}' must be one of: {string.Join(", ", parameter.Options)}.";
                return false;
            }
        }
        else if (parameter.IsBool)
        {
            if (!bool.TryParse(value, out var boolean))
            {
                error = $"Parameter '{parameter.Name}' must be true or false.";
                return false;
            }

            normalized = boolean ? "true" : "false";
        }
        else if (parameter.IsInt)
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
            {
                error = $"Parameter '{parameter.Name}' must be an integer.";
                return false;
            }

            if (parameter.Min is { } min && integer < min)
            {
                error = $"Parameter '{parameter.Name}' must be at least {min}.";
                return false;
            }

            if (parameter.Max is { } max && integer > max)
            {
                error = $"Parameter '{parameter.Name}' must be at most {max}.";
                return false;
            }

            normalized = integer.ToString(CultureInfo.InvariantCulture);
        }
        else if (parameter.IsFile)
        {
            try
            {
                normalized = Path.GetFullPath(value);
                if (File.Exists(normalized))
                {
                    return true;
                }
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
            {
                // Report invalid or inaccessible paths as a parameter error, without launching anything.
            }

            error = $"Parameter '{parameter.Name}' must be an existing file.";
            return false;
        }
        else if (!string.Equals(parameter.Type, ScriptParameter.ParameterTypeString, StringComparison.OrdinalIgnoreCase))
        {
            error = $"Parameter '{parameter.Name}' has an unsupported type '{parameter.Type}'.";
            return false;
        }

        return true;
    }
}
