// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;
using System.Text.RegularExpressions;

namespace PowerScripts.Core.Manifest;

/// <summary>Validates the supported schema profile and invocation data without executing code.</summary>
public static class ScriptSchema
{
    public static void ValidateDefinition(JsonSchemaObject? schema, string path)
    {
        if (schema is null)
        {
            throw new InvalidDataException($"'{path}' is required.");
        }

        if (schema.Type is not ("object" or "array" or "string" or "integer" or "boolean"))
        {
            throw new InvalidDataException($"'{path}.type' must be object, array, string, integer, or boolean.");
        }

        if (schema.MinItems < 0 || schema.MaxItems < 0 || schema.MaxItems < schema.MinItems ||
            (schema.Type == "array" && schema.Items is null))
        {
            throw new InvalidDataException($"Invalid array constraints at '{path}'.");
        }

        if (schema.Required is { } required &&
            (required.Distinct(StringComparer.Ordinal).Count() != required.Count ||
                required.Any(name => schema.Properties?.ContainsKey(name) != true)))
        {
            throw new InvalidDataException($"'{path}.required' must name declared properties without duplicates.");
        }

        if (schema.Type == "object" && schema.AdditionalProperties != false)
        {
            throw new InvalidDataException($"'{path}' must be a closed object.");
        }

        if (schema.Properties is { } properties)
        {
            if (schema.Type != "object" || schema.AdditionalProperties != false)
            {
                throw new InvalidDataException($"'{path}' must be a closed object.");
            }

            foreach (var (name, child) in properties)
            {
                ValidateDefinition(child, $"{path}.{name}");
            }
        }

        if (schema.Items is { } items)
        {
            ValidateDefinition(items, path + ".items");
        }

        if (schema.PowerScript is { Extensions: { } extensions } &&
            (schema.Type != "string" || extensions.Count == 0 ||
                extensions.Any(extension => extension is null || (extension != "*" &&
                    (!extension.StartsWith('.') || extension.Length < 2 ||
                        extension.IndexOfAny(new[] { '\\', '/', '*', '?', '\0' }) >= 0)))))
        {
            throw new InvalidDataException($"'{path}.x-powerscript.extensions' must contain '*' or filename extensions such as '.txt'.");
        }

        if (schema is JsonSchemaProperty property)
        {
            if (property.MinLength < 0 || property.MaxLength < 0 || property.MaxLength < property.MinLength ||
                property.Maximum < property.Minimum)
            {
                throw new InvalidDataException($"Invalid bounds at '{path}'.");
            }

            if (property.Pattern is { } pattern)
            {
                _ = new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            }

            if ((property.Enum is not null && (property.Type != "string" || property.Enum.Count == 0)) ||
                (property.Format is not null && (property.Type != "string" || !property.IsFileParameter)))
            {
                throw new InvalidDataException($"'{path}' supports string enums and the string format 'file-path' only.");
            }

            if (property.Default.ValueKind != JsonValueKind.Undefined)
            {
                Validate(schema, property.Default, path + ".default");
            }
        }
    }

    public static void ApplyDefaults(JsonSchemaObject schema, JsonObject value)
    {
        foreach (var (name, child) in schema.Properties ?? new())
        {
            if (!value.ContainsKey(name) && child.Default.ValueKind != JsonValueKind.Undefined)
            {
                value[name] = JsonNode.Parse(child.Default.GetRawText());
            }

            if (value[name] is JsonObject nested)
            {
                ApplyDefaults(child, nested);
            }
        }
    }

    public static void Validate(JsonSchemaObject schema, JsonElement value, string path)
    {
        var validType = schema.Type switch
        {
            "object" => value.ValueKind == JsonValueKind.Object,
            "array" => value.ValueKind == JsonValueKind.Array,
            "string" => value.ValueKind == JsonValueKind.String,
            "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _),
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            _ => false,
        };
        if (!validType)
        {
            throw new InvalidDataException($"'{path}' must be {schema.Type}.");
        }

        if (schema.Type == "object")
        {
            foreach (var name in schema.Required ?? new())
            {
                if (!value.TryGetProperty(name, out _))
                {
                    throw new InvalidDataException($"Missing required property '{path}.{name}'.");
                }
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!seen.Add(property.Name))
                {
                    throw new InvalidDataException($"Duplicate property '{path}.{property.Name}'.");
                }

                if (schema.Properties?.TryGetValue(property.Name, out var child) == true)
                {
                    Validate(child, property.Value, $"{path}.{property.Name}");
                }
                else if (schema.AdditionalProperties == false)
                {
                    throw new InvalidDataException($"Unknown property '{path}.{property.Name}'.");
                }
            }
        }
        else if (schema.Type == "array")
        {
            var count = value.GetArrayLength();
            if ((schema.MinItems is { } minimum && count < minimum) ||
                (schema.MaxItems is { } maximum && count > maximum))
            {
                throw new InvalidDataException($"'{path}' does not satisfy its file-count limits.");
            }

            foreach (var item in value.EnumerateArray())
            {
                Validate(schema.Items!, item, path + "[]");
            }
        }

        if (schema is JsonSchemaProperty node)
        {
            if (node.Enum is { } choices &&
                (value.ValueKind != JsonValueKind.String || !choices.Contains(value.GetString(), StringComparer.Ordinal)))
            {
                throw new InvalidDataException($"'{path}' must be one of: {string.Join(", ", choices)}.");
            }

            if (value.ValueKind == JsonValueKind.Number &&
                ((node.Minimum is { } minimum && value.GetInt32() < minimum) ||
                (node.Maximum is { } maximum && value.GetInt32() > maximum)))
            {
                throw new InvalidDataException($"'{path}' is outside its numeric bounds.");
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString()!;
                var length = text.EnumerateRunes().Count();
                if ((node.MinLength is { } minLength && length < minLength) ||
                    (node.MaxLength is { } maxLength && length > maxLength) ||
                    (node.Pattern is { } pattern && !Regex.IsMatch(text, pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))))
                {
                    throw new InvalidDataException($"'{path}' does not satisfy its string constraints.");
                }
            }
        }

        if (schema.PowerScript?.Extensions is { } extensions)
        {
            var extension = Path.GetExtension(value.GetString()!);
            if (!extensions.Contains("*", StringComparer.Ordinal) &&
                !extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"'{path}' must have one of these extensions: {string.Join(", ", extensions)}.");
            }
        }
    }
}
