// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text.Json;

namespace PowerScripts.Client;

/// <summary>Builds typed consumer input while keeping omitted values distinct from supplied values.</summary>
public static class PowerScriptParameterValues
{
    /// <summary>
    /// Converts the supplied parameter text to the types advertised by discovery. Missing values
    /// stay omitted, allowing the Host to apply defaults. The Host remains the execution authority.
    /// </summary>
    public static bool TryCreate(
        PowerScriptInfo script,
        IReadOnlyDictionary<string, string?> providedValues,
        out IReadOnlyDictionary<string, JsonElement> values,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(providedValues);
        values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        error = null;
        var declarations = new Dictionary<string, PowerScriptParameter>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in script.Parameters)
        {
            if (string.IsNullOrWhiteSpace(parameter.Name) || !declarations.TryAdd(parameter.Name, parameter))
            {
                error = "The script declares an empty or duplicate parameter name.";
                return false;
            }
        }

        var supplied = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in providedValues)
        {
            if (!declarations.ContainsKey(name))
            {
                error = $"Unknown parameter '{name}' for script '{script.Id}'.";
                return false;
            }

            if (!supplied.TryAdd(name, value))
            {
                error = $"Parameter '{name}' was supplied more than once.";
                return false;
            }
        }

        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var parameter in script.Parameters)
        {
            if (!supplied.TryGetValue(parameter.Name, out var text))
            {
                if (parameter.IsRequired && parameter.Default is null)
                {
                    error = $"Missing required parameter '{parameter.Name}'.";
                    return false;
                }

                continue;
            }

            if (text is null)
            {
                error = $"Parameter '{parameter.Name}' cannot be null.";
                return false;
            }

            var type = NormalizeType(parameter.Type);
            if (parameter.IsRequired && (type is "string" or "file") && string.IsNullOrWhiteSpace(text))
            {
                error = $"Missing required parameter '{parameter.Name}'.";
                return false;
            }

            JsonElement value;
            switch (type)
            {
                case "string":
                case "file":
                case "choice":
                    if (type == "choice" && !parameter.Options.Contains(text, StringComparer.Ordinal))
                    {
                        error = $"Parameter '{parameter.Name}' must be one of: {string.Join(", ", parameter.Options)}.";
                        return false;
                    }

                    value = CreateJsonValue(writer => writer.WriteStringValue(text));
                    break;
                case "int":
                    if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
                    {
                        error = $"Parameter '{parameter.Name}' must be an integer.";
                        return false;
                    }

                    if (parameter.Min is int minimum && integer < minimum)
                    {
                        error = $"Parameter '{parameter.Name}' must be at least {minimum}.";
                        return false;
                    }

                    if (parameter.Max is int maximum && integer > maximum)
                    {
                        error = $"Parameter '{parameter.Name}' must be at most {maximum}.";
                        return false;
                    }

                    value = CreateJsonValue(writer => writer.WriteNumberValue(integer));
                    break;
                case "bool":
                    if (!bool.TryParse(text, out var boolean))
                    {
                        error = $"Parameter '{parameter.Name}' must be true or false.";
                        return false;
                    }

                    value = CreateJsonValue(writer => writer.WriteBooleanValue(boolean));
                    break;
                default:
                    error = $"Parameter '{parameter.Name}' has unsupported type '{parameter.Type}'.";
                    return false;
            }

            result.Add(parameter.Name, value);
        }

        values = result;
        return true;
    }

    /// <summary>Compares input contracts while ignoring display titles, descriptions and property order.</summary>
    public static bool HasSameInputContract(PowerScriptInfo previous, PowerScriptInfo current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);
        if (previous.Input != current.Input || previous.Parameters.Count != current.Parameters.Count)
        {
            return false;
        }

        var currentByName = new Dictionary<string, PowerScriptParameter>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in current.Parameters)
        {
            if (string.IsNullOrWhiteSpace(parameter.Name) || !currentByName.TryAdd(parameter.Name, parameter))
            {
                return false;
            }
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var left in previous.Parameters)
        {
            if (string.IsNullOrWhiteSpace(left.Name) || !seen.Add(left.Name) ||
                !currentByName.TryGetValue(left.Name, out var right) || left.Name != right.Name ||
                NormalizeType(left.Type) != NormalizeType(right.Type) ||
                left.IsRequired != right.IsRequired || left.Default != right.Default ||
                left.Min != right.Min || left.Max != right.Max ||
                !left.Options.SequenceEqual(right.Options, StringComparer.Ordinal))
            {
                return false;
            }
        }

        return previous.InputSchema is JsonElement previousSchema
            ? current.InputSchema is JsonElement currentSchema && SameSchema(previousSchema, currentSchema)
            : current.InputSchema is null;
    }

    private static string NormalizeType(string type) => type?.ToLowerInvariant() switch
    {
        "integer" => "int",
        "boolean" => "bool",
        "file-path" => "file",
        var value => value ?? string.Empty,
    };

    private static JsonElement CreateJsonValue(Action<Utf8JsonWriter> write)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            write(writer);
        }

        using var document = JsonDocument.Parse(buffer.ToArray());
        return document.RootElement.Clone();
    }

    private static bool SameSchema(JsonElement left, JsonElement right, bool propertyMap = false)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return false;
        }

        if (left.ValueKind == JsonValueKind.Object)
        {
            var leftProperties = SchemaProperties(left, propertyMap);
            var rightProperties = SchemaProperties(right, propertyMap);
            return leftProperties is not null && rightProperties is not null &&
                leftProperties.Count == rightProperties.Count &&
                leftProperties.All(property => rightProperties.TryGetValue(property.Key, out var value) &&
                    SameSchema(property.Value, value, !propertyMap && property.Key == "properties"));
        }

        if (left.ValueKind == JsonValueKind.Array)
        {
            if (left.GetArrayLength() != right.GetArrayLength())
            {
                return false;
            }

            for (var index = 0; index < left.GetArrayLength(); index++)
            {
                if (!SameSchema(left[index], right[index]))
                {
                    return false;
                }
            }

            return true;
        }

        return left.ValueKind switch
        {
            JsonValueKind.String => string.Equals(left.GetString(), right.GetString(), StringComparison.Ordinal),
            JsonValueKind.Number => left.TryGetDecimal(out var leftNumber) && right.TryGetDecimal(out var rightNumber)
                ? leftNumber == rightNumber
                : left.GetRawText() == right.GetRawText(),
            _ => true,
        };
    }

    private static Dictionary<string, JsonElement>? SchemaProperties(JsonElement schema, bool propertyMap)
    {
        var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in schema.EnumerateObject())
        {
            if (!propertyMap && (property.Name is "title" or "description"))
            {
                continue;
            }

            if (!properties.TryAdd(property.Name, property.Value))
            {
                return null;
            }
        }

        return properties;
    }
}
