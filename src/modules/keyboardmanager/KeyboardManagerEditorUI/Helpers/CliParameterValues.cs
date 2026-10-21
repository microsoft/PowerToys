// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using PowerScripts.Client;

namespace KeyboardManagerEditorUI.Helpers
{
    /// <summary>Validates parameter drafts against the shared Host parameter contract.</summary>
    internal static class CliParameterValues
    {
        /// <summary>
        /// Applies defaults to absent values and normalizes integers and booleans. File existence
        /// remains an execution-time check in the Host.
        /// </summary>
        public static bool TryValidate(
            IReadOnlyList<PowerScriptParameter> definitions,
            IReadOnlyDictionary<string, string?> values,
            out IReadOnlyDictionary<string, string?> normalizedValues,
            out string? errorKey,
            out string? parameterName)
        {
            ArgumentNullException.ThrowIfNull(definitions);
            ArgumentNullException.ThrowIfNull(values);

            normalizedValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            errorKey = null;
            parameterName = null;

            var declarations = new Dictionary<string, PowerScriptParameter>(StringComparer.OrdinalIgnoreCase);
            foreach (var parameter in definitions)
            {
                if (string.IsNullOrWhiteSpace(parameter.Name) || !declarations.TryAdd(parameter.Name, parameter))
                {
                    errorKey = "CliParameterUnknown";
                    parameterName = parameter.Name;
                    return false;
                }

                if (!IsSupportedType(parameter.Type))
                {
                    errorKey = "CliParameterUnsupportedType";
                    parameterName = parameter.Name;
                    return false;
                }
            }

            var supplied = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (name, value) in values)
            {
                if (!declarations.ContainsKey(name) || !supplied.TryAdd(name, value))
                {
                    errorKey = "CliParameterUnknown";
                    parameterName = name;
                    return false;
                }
            }

            var normalized = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var parameter in definitions)
            {
                bool hasSuppliedValue = supplied.TryGetValue(parameter.Name, out var value);
                value = hasSuppliedValue ? value : parameter.Default;

                if (string.IsNullOrEmpty(value))
                {
                    if (parameter.IsRequired)
                    {
                        errorKey = "CliParameterRequired";
                        parameterName = parameter.Name;
                        return false;
                    }

                    bool validatesEmptyValue = parameter.Type.Equals("int", StringComparison.OrdinalIgnoreCase)
                        || parameter.Type.Equals("bool", StringComparison.OrdinalIgnoreCase)
                        || parameter.Type.Equals("choice", StringComparison.OrdinalIgnoreCase);
                    if (!hasSuppliedValue || !validatesEmptyValue)
                    {
                        if (hasSuppliedValue || parameter.Default is not null)
                        {
                            normalized[parameter.Name] = value;
                        }

                        continue;
                    }
                }

                if (!TryNormalizeValue(parameter, value ?? string.Empty, out var normalizedValue, out errorKey))
                {
                    parameterName = parameter.Name;
                    return false;
                }

                normalized[parameter.Name] = normalizedValue;
            }

            normalizedValues = normalized;
            return true;
        }

        private static bool IsSupportedType(string? type)
        {
            return type?.ToLowerInvariant() is "string" or "int" or "bool" or "choice" or "file";
        }

        private static bool TryNormalizeValue(
            PowerScriptParameter parameter,
            string value,
            out string normalizedValue,
            out string? errorKey)
        {
            normalizedValue = value;
            errorKey = null;

            if (value.Contains('\0'))
            {
                errorKey = "CliParameterInvalidValue";
                return false;
            }

            switch (parameter.Type.ToLowerInvariant())
            {
                case "string":
                    return true;

                case "file":
                    try
                    {
                        // Pin relative paths to the same location used by a test from this editor.
                        // The background engine can have a different working directory later.
                        normalizedValue = Path.GetFullPath(value);
                        return true;
                    }
                    catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
                    {
                        errorKey = "CliParameterInvalidFile";
                        return false;
                    }

                case "int":
                    if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                    {
                        errorKey = "CliParameterInvalidInteger";
                        return false;
                    }

                    if ((parameter.Min is { } min && number < min)
                        || (parameter.Max is { } max && number > max))
                    {
                        errorKey = "CliParameterOutOfRange";
                        return false;
                    }

                    normalizedValue = number.ToString(CultureInfo.InvariantCulture);
                    return true;

                case "bool":
                    if (!bool.TryParse(value, out var boolean))
                    {
                        errorKey = "CliParameterInvalidBoolean";
                        return false;
                    }

                    normalizedValue = boolean ? "true" : "false";
                    return true;

                case "choice":
                    if (!parameter.Options.Contains(value, StringComparer.Ordinal))
                    {
                        errorKey = "CliParameterInvalidChoice";
                        return false;
                    }

                    return true;

                default:
                    errorKey = "CliParameterUnsupportedType";
                    return false;
            }
        }
    }
}
