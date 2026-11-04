// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using PowerScripts.Client;

namespace FileExplorerScripts.Core;

public static class ScriptParameters
{
    public static IReadOnlyDictionary<string, JsonElement> GetDefaults(PowerScriptInfo script) =>
        Parse(script, new Dictionary<string, string?>());

    public static bool CanRunWithDefaults(PowerScriptInfo script)
    {
        ArgumentNullException.ThrowIfNull(script);
        try
        {
            GetDefaults(script);
            return true;
        }
        catch (ParameterValidationException)
        {
            return false;
        }
    }

    public static IReadOnlyDictionary<string, JsonElement> Parse(
        PowerScriptInfo script,
        IReadOnlyDictionary<string, string?> values)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(values);
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var parameter in script.Parameters)
        {
            if (string.IsNullOrWhiteSpace(parameter.Name) || !names.Add(parameter.Name))
            {
                throw Invalid(parameter, "has an empty or duplicate parameter name; repair the script metadata.");
            }

            if (parameter.Type is not ("string" or "int" or "bool" or "choice" or "file"))
            {
                throw Invalid(parameter, $"uses unsupported type '{parameter.Type}'.");
            }

            if (parameter.Min > parameter.Max)
            {
                throw Invalid(parameter, "has a minimum greater than its maximum; repair the script metadata.");
            }

            if (parameter.Type == "choice" && parameter.Options.Count == 0)
            {
                throw Invalid(parameter, "has no choices; repair the script metadata.");
            }

            var value = values.TryGetValue(parameter.Name, out var supplied) ? supplied : parameter.Default;
            if (string.IsNullOrEmpty(value))
            {
                if (parameter.IsRequired)
                {
                    throw Invalid(parameter, "is required; enter a value or configure a valid default.");
                }

                continue;
            }

            result.Add(parameter.Name, ParseValue(parameter, value));
        }

        foreach (var name in values.Keys)
        {
            if (!names.Contains(name))
            {
                throw new ParameterValidationException($"Parameter '{name}' is no longer declared by this script; refresh its parameters.");
            }
        }

        return new ReadOnlyDictionary<string, JsonElement>(result);
    }

    internal static IReadOnlyDictionary<string, JsonElement> ValidateTyped(
        PowerScriptInfo script,
        IReadOnlyDictionary<string, JsonElement> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var raw = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (name, value) in values)
        {
            var parameter = script.Parameters.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.Ordinal))
                ?? throw new ParameterValidationException($"Parameter '{name}' is no longer declared; refresh its parameters.");
            var validKind = parameter.Type switch
            {
                "bool" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                "int" => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _),
                _ => value.ValueKind == JsonValueKind.String,
            };
            if (!validKind)
            {
                throw Invalid(parameter, $"must be a JSON {parameter.Type} value; refresh its parameters.");
            }

            raw.Add(name, value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText());
        }

        return Parse(script, raw);
    }

    private static JsonElement ParseValue(PowerScriptParameter parameter, string value)
    {
        switch (parameter.Type)
        {
            case "int":
                if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number))
                {
                    throw Invalid(parameter, "must be a whole 32-bit integer without whitespace or decimal separators.");
                }

                if (number < parameter.Min || number > parameter.Max)
                {
                    throw Invalid(parameter, $"must be between {parameter.Min?.ToString(CultureInfo.InvariantCulture) ?? int.MinValue.ToString(CultureInfo.InvariantCulture)} and {parameter.Max?.ToString(CultureInfo.InvariantCulture) ?? int.MaxValue.ToString(CultureInfo.InvariantCulture)}.");
                }

                return JsonSerializer.SerializeToElement(number);
            case "bool":
                if (value is not ("true" or "false"))
                {
                    throw Invalid(parameter, "must be 'true' or 'false' (not 0, 1, or yes/no).");
                }

                return JsonSerializer.SerializeToElement(value == "true");
            case "choice":
                if (!parameter.Options.Contains(value, StringComparer.Ordinal))
                {
                    throw Invalid(parameter, "must match one of its declared choices exactly.");
                }

                break;
            case "file":
                try
                {
                    LocalPaths.Validate(value, directory: false);
                }
                catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
                {
                    throw Invalid(parameter, "must name an existing local regular file, without reparse points: " + ex.Message);
                }

                break;
            case "string":
                if (parameter.IsRequired && string.IsNullOrWhiteSpace(value))
                {
                    throw Invalid(parameter, "is required; enter non-whitespace text.");
                }

                break;
        }

        return JsonSerializer.SerializeToElement(value);
    }

    private static ParameterValidationException Invalid(PowerScriptParameter parameter, string reason) =>
        new($"'{(string.IsNullOrWhiteSpace(parameter.Label) ? parameter.Name : parameter.Label)}' {reason}");

    private sealed class ParameterValidationException(string message) : ArgumentException(message);
}
