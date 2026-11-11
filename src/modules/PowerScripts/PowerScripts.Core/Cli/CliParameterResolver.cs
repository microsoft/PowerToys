// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text.Json;
using PowerScripts.Core.Manifest;

namespace PowerScripts.Core.Cli;

internal static class CliParameterResolver
{
    internal static bool TryReadConstant(
        ScriptParameter parameter,
        JsonElement constant,
        out string value,
        out string error)
    {
        value = string.Empty;
        error = string.Empty;
        var matches = parameter.IsBool
            ? constant.ValueKind is JsonValueKind.True or JsonValueKind.False
            : parameter.IsInt
                ? constant.ValueKind == JsonValueKind.Number && constant.TryGetInt32(out _)
                : constant.ValueKind == JsonValueKind.String;
        if (!matches)
        {
            error = $"Constant for '{parameter.Name}' must use the command's declared JSON type.";
            return false;
        }

        value = constant.ValueKind switch
        {
            JsonValueKind.String => constant.GetString()!,
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => constant.GetRawText(),
        };
        return TryNormalize(parameter, value, requireExistingFile: false, out value, out error);
    }

    internal static bool TryNormalize(
        ScriptParameter parameter,
        string value,
        bool requireExistingFile,
        out string normalized,
        out string error)
    {
        normalized = value;
        error = string.Empty;
        if (value.Contains('\0'))
        {
            error = $"Parameter '{parameter.Name}' cannot contain a NUL character.";
            return false;
        }

        if (parameter.IsRequired && string.IsNullOrWhiteSpace(value))
        {
            error = $"Missing required parameter '{parameter.Name}'.";
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
                error = $"Parameter '{parameter.Name}' must be a 32-bit integer.";
                return false;
            }

            if ((parameter.Min is { } min && integer < min) || (parameter.Max is { } max && integer > max))
            {
                error = $"Parameter '{parameter.Name}' is outside the command's declared bounds.";
                return false;
            }

            normalized = integer.ToString(CultureInfo.InvariantCulture);
        }
        else if (parameter.IsFile)
        {
            if (!requireExistingFile)
            {
                return true;
            }

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
                // Return a parameter error without attempting a launch.
            }

            error = $"Parameter '{parameter.Name}' must be an existing file.";
            return false;
        }
        else if (parameter.Type != ScriptParameter.ParameterTypeString)
        {
            error = $"Parameter '{parameter.Name}' has an unsupported type.";
            return false;
        }

        return true;
    }
}
