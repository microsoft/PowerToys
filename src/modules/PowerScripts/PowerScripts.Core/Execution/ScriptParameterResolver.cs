// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.IO;
using PowerScripts.Core.Manifest;

namespace PowerScripts.Core.Execution;

/// <summary>Validates consumer-provided parameter values and applies declared defaults.</summary>
public static class ScriptParameterResolver
{
    public static bool TryResolve(
        PowerScriptManifest manifest,
        IReadOnlyDictionary<string, string?> supplied,
        out Dictionary<string, string?> resolved,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(supplied);

        resolved = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        error = string.Empty;
        var declarations = manifest.Parameters.ToDictionary(parameter => parameter.Name, StringComparer.OrdinalIgnoreCase);
        var suppliedByName = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, value) in supplied)
        {
            if (!declarations.ContainsKey(name))
            {
                error = $"Unknown parameter '{name}' for script '{manifest.Id}'.";
                return false;
            }

            if (!suppliedByName.TryAdd(name, value))
            {
                error = $"Parameter '{name}' was supplied more than once.";
                return false;
            }
        }

        foreach (var parameter in manifest.Parameters)
        {
            var hasSuppliedValue = suppliedByName.TryGetValue(parameter.Name, out var value);
            value = hasSuppliedValue ? value : parameter.Default;

            if (string.IsNullOrEmpty(value))
            {
                if (parameter.IsRequired)
                {
                    error = $"Missing required parameter '{parameter.Name}'.";
                    return false;
                }

                if (hasSuppliedValue && (parameter.IsChoice || parameter.IsBool || parameter.IsInt))
                {
                    return IsValid(parameter, value ?? string.Empty, out error);
                }

                if (hasSuppliedValue || parameter.Default is not null)
                {
                    resolved[parameter.Name] = value;
                }

                continue;
            }

            if (!IsValid(parameter, value, out error))
            {
                return false;
            }

            resolved[parameter.Name] = value;
        }

        return true;
    }

    private static bool IsValid(ScriptParameter parameter, string value, out string error)
    {
        error = string.Empty;
        if (parameter.IsChoice && !parameter.Options.Contains(value, StringComparer.Ordinal))
        {
            error = $"Parameter '{parameter.Name}' must be one of: {string.Join(", ", parameter.Options)}.";
            return false;
        }

        if (parameter.IsBool && !bool.TryParse(value, out _))
        {
            error = $"Parameter '{parameter.Name}' must be true or false.";
            return false;
        }

        if (parameter.IsInt)
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                error = $"Parameter '{parameter.Name}' must be an integer.";
                return false;
            }

            if (parameter.Min is { } min && number < min)
            {
                error = $"Parameter '{parameter.Name}' must be at least {min}.";
                return false;
            }

            if (parameter.Max is { } max && number > max)
            {
                error = $"Parameter '{parameter.Name}' must be at most {max}.";
                return false;
            }
        }

        if (parameter.IsFile && !File.Exists(value))
        {
            error = $"Parameter '{parameter.Name}' must be an existing file.";
            return false;
        }

        return true;
    }
}
