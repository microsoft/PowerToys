// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PowerScripts.Client;

namespace Microsoft.CmdPal.Ext.PowerScripts;

/// <summary>Converts form fields to the typed parameter values accepted by the shared host.</summary>
internal static class PowerScriptParameterValues
{
    private static readonly TimeSpan PatternTimeout = TimeSpan.FromSeconds(1);

    internal static IReadOnlyDictionary<string, string> CreateInitialValues(PowerScriptInfo script)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < script.Parameters.Count; index++)
        {
            var parameter = script.Parameters[index];
            var suffix = index.ToString(CultureInfo.InvariantCulture);
            values["include_" + suffix] = parameter.IsRequired || parameter.Default is not null ? "true" : "false";

            var value = parameter.Default ?? (string.Equals(parameter.Type, "bool", StringComparison.OrdinalIgnoreCase) ? "false" : string.Empty);
            if (string.Equals(parameter.Type, "bool", StringComparison.OrdinalIgnoreCase) && bool.TryParse(value, out var boolean))
            {
                value = boolean ? "true" : "false";
            }

            values["parameter_" + suffix] = value;
        }

        return values;
    }

    internal static bool TryRead(
        PowerScriptInfo script,
        IReadOnlyDictionary<string, string> input,
        out IReadOnlyDictionary<string, JsonElement> values,
        out IReadOnlyDictionary<string, string> errors)
    {
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var validationErrors = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < script.Parameters.Count; index++)
        {
            var parameter = script.Parameters[index];
            var suffix = index.ToString(CultureInfo.InvariantCulture);
            var field = "parameter_" + suffix;
            if (!parameter.IsRequired)
            {
                if (!input.TryGetValue("include_" + suffix, out var includeText))
                {
                    continue;
                }

                if (!bool.TryParse(includeText, out var include))
                {
                    validationErrors[field] = Properties.Resources.InvalidBoolean;
                    continue;
                }

                if (!include)
                {
                    continue;
                }
            }

            if (!input.TryGetValue(field, out var value))
            {
                validationErrors[field] = Properties.Resources.RequiredValue;
                continue;
            }

            switch (parameter.Type.ToLowerInvariant())
            {
                case "int":
                    if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
                    {
                        validationErrors[field] = Properties.Resources.InvalidInteger;
                    }
                    else if (parameter.Min is { } minimum && integer < minimum)
                    {
                        validationErrors[field] = PowerScriptFormatting.Format(Properties.Resources.MinValue, minimum);
                    }
                    else if (parameter.Max is { } maximum && integer > maximum)
                    {
                        validationErrors[field] = PowerScriptFormatting.Format(Properties.Resources.MaxValue, maximum);
                    }
                    else
                    {
                        result[parameter.Name] = ToJson(integer);
                    }

                    break;

                case "bool":
                    if (bool.TryParse(value, out var boolean))
                    {
                        result[parameter.Name] = ToJson(boolean);
                    }
                    else
                    {
                        validationErrors[field] = Properties.Resources.InvalidBoolean;
                    }

                    break;

                case "choice":
                    if (parameter.Options.Contains(value, StringComparer.Ordinal))
                    {
                        result[parameter.Name] = ToJson(value);
                    }
                    else
                    {
                        validationErrors[field] = Properties.Resources.InvalidChoice;
                    }

                    break;

                case "string":
                case "file":
                    if (!TryValidateString(value, GetParameterSchema(script, parameter.Name), out var error))
                    {
                        validationErrors[field] = error;
                    }
                    else if (string.Equals(parameter.Type, "file", StringComparison.OrdinalIgnoreCase) &&
                        (!Path.IsPathFullyQualified(value) || !File.Exists(value)))
                    {
                        validationErrors[field] = Properties.Resources.FileNotFound;
                    }
                    else
                    {
                        result[parameter.Name] = ToJson(value);
                    }

                    break;

                default:
                    validationErrors[field] = Properties.Resources.UnsupportedParameterType;
                    break;
            }
        }

        values = result;
        errors = validationErrors;
        return validationErrors.Count == 0;
    }

    private static JsonElement? GetParameterSchema(PowerScriptInfo script, string name)
    {
        if (script.InputSchema is not JsonElement schema || schema.ValueKind != JsonValueKind.Object ||
            !schema.TryGetProperty("properties", out var properties) || properties.ValueKind != JsonValueKind.Object ||
            !properties.TryGetProperty("parameters", out var parameters) || parameters.ValueKind != JsonValueKind.Object ||
            !parameters.TryGetProperty("properties", out var parameterProperties) || parameterProperties.ValueKind != JsonValueKind.Object ||
            !parameterProperties.TryGetProperty(name, out var parameter) || parameter.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return parameter;
    }

    private static bool TryValidateString(string value, JsonElement? schema, out string error)
    {
        error = string.Empty;
        if (schema is not JsonElement constraints)
        {
            return true;
        }

        // JSON Schema counts Unicode scalar values, rather than UTF-16 code units.
        var length = value.EnumerateRunes().Count();
        if (constraints.TryGetProperty("minLength", out var minLength) && minLength.ValueKind == JsonValueKind.Number &&
            minLength.TryGetInt32(out var minimum) && length < minimum)
        {
            error = PowerScriptFormatting.Format(Properties.Resources.MinLength, minimum);
            return false;
        }

        if (constraints.TryGetProperty("maxLength", out var maxLength) && maxLength.ValueKind == JsonValueKind.Number &&
            maxLength.TryGetInt32(out var maximum) && length > maximum)
        {
            error = PowerScriptFormatting.Format(Properties.Resources.MaxLength, maximum);
            return false;
        }

        if (constraints.TryGetProperty("pattern", out var pattern) && pattern.ValueKind == JsonValueKind.String)
        {
            try
            {
                if (!Regex.IsMatch(value, pattern.GetString()!, RegexOptions.CultureInvariant, PatternTimeout))
                {
                    error = Properties.Resources.PatternMismatch;
                    return false;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                error = Properties.Resources.InvalidPattern;
                return false;
            }
            catch (ArgumentException)
            {
                error = Properties.Resources.InvalidPattern;
                return false;
            }
        }

        return true;
    }

    private static JsonElement ToJson(string value)
    {
        using var document = JsonDocument.Parse("\"" + JsonEncodedText.Encode(value) + "\"");
        return document.RootElement.Clone();
    }

    private static JsonElement ToJson(int value)
    {
        using var document = JsonDocument.Parse(value.ToString(CultureInfo.InvariantCulture));
        return document.RootElement.Clone();
    }

    private static JsonElement ToJson(bool value)
    {
        using var document = JsonDocument.Parse(value ? "true" : "false");
        return document.RootElement.Clone();
    }
}
