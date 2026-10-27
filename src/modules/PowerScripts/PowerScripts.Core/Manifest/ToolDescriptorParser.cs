// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using PowerScripts.Core.Execution;

namespace PowerScripts.Core.Manifest;

/// <summary>Loads the schema-authoritative PowerScripts profile of an MCP tool.</summary>
public static class ToolDescriptorParser
{
    public const string DescriptorSuffix = ".tool.json";

    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        MaxDepth = 32,
    };

    public static bool IsDescriptorFile(string path) =>
        path.EndsWith(DescriptorSuffix, StringComparison.OrdinalIgnoreCase);

    public static PowerScriptManifest? TryParseFile(string descriptorPath)
    {
        if (!IsDescriptorFile(descriptorPath))
        {
            return null;
        }

        return Parse(
            JsonSerializer.Deserialize<ToolDescriptor>(File.ReadAllText(descriptorPath), JsonOptions),
            Path.GetFileName(descriptorPath[..^DescriptorSuffix.Length]));
    }

    public static PowerScriptManifest? Parse(ToolDescriptor? descriptor, string entry)
    {
        if (descriptor is null || string.IsNullOrWhiteSpace(descriptor.Name))
        {
            throw new InvalidDataException("The descriptor requires a non-empty 'name'.");
        }

        var runtime = Path.GetExtension(entry).ToLowerInvariant() switch
        {
            ".py" => ScriptRuntime.Python,
            ".ps1" => ScriptRuntime.PowerShell,
            _ when descriptor.Execute is not null => ScriptRuntime.PowerShell,
            _ => throw new InvalidDataException("Script entries must be .py or .ps1 unless an explicit launch recipe is supplied."),
        };
        ScriptSchema.ValidateDefinition(descriptor.InputSchema, "inputSchema");
        ScriptSchema.ValidateDefinition(descriptor.OutputSchema, "outputSchema");
        var input = ResolveShape(descriptor.InputSchema!, isInput: true);
        var output = ResolveShape(descriptor.OutputSchema!, isInput: false);
        if (input is PowerScriptDataFormat.Image or PowerScriptDataFormat.Audio &&
            descriptor.Execute is { UsesJsonStdin: false })
        {
            throw new InvalidDataException("Inline media requires 'x-execute.stdin': 'json' when using an explicit launch recipe.");
        }

        var extension = descriptor.PowerScript;
        var manifest = new PowerScriptManifest
        {
            Id = descriptor.Name.Trim(),
            Name = FirstNonEmpty(descriptor.Title, descriptor.Annotations?.Title, descriptor.Name)!.Trim(),
            Description = descriptor.Description?.Trim() ?? string.Empty,
            Entry = entry,
            Runtime = runtime,
            Execute = descriptor.Execute,
            InputSchema = descriptor.InputSchema!,
            OutputSchema = descriptor.OutputSchema!,
            InputFormat = input,
            OutputFormat = output,
            Publisher = extension?.Publisher,
            Version = extension?.Version,
            Source = extension?.Source,
            Icon = extension?.Icon,
            Consumers = extension?.Consumers,
            Capabilities = extension?.Capabilities ?? new(),
        };
        if (input == PowerScriptDataFormat.Files)
        {
            var files = descriptor.InputSchema!.Properties!["files"];
            manifest.Input = new ScriptInput
            {
                Extensions = files.Items!.PowerScript?.Extensions ?? new List<string> { "*" },
                MinFiles = files.MinItems ?? 0,
                MaxFiles = files.MaxItems,
            };
        }

        if (descriptor.InputSchema!.Properties?.TryGetValue("parameters", out var parameters) == true)
        {
            if (parameters.Type != "object")
            {
                throw new InvalidDataException("'inputSchema.properties.parameters' must be an object.");
            }

            foreach (var (name, schema) in parameters.Properties ?? new())
            {
                if (input != PowerScriptDataFormat.None && name.Equals(ScriptIo.ToToken(input), StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"Parameter '{name}' conflicts with the primary input binding.");
                }

                var type = schema.IsFileParameter ? ScriptParameter.ParameterTypeFile :
                    schema.Enum is { Count: > 0 } ? ScriptParameter.ParameterTypeChoice :
                    schema.Type switch
                    {
                        "string" => ScriptParameter.ParameterTypeString,
                        "integer" => ScriptParameter.ParameterTypeInt,
                        "boolean" => ScriptParameter.ParameterTypeBool,
                        _ => throw new InvalidDataException($"Parameter '{name}' must be a string, integer, or boolean."),
                    };
                manifest.Parameters.Add(new ScriptParameter
                {
                    Name = name,
                    Label = schema.Title,
                    Description = schema.Description,
                    Type = type,
                    Default = schema.DefaultAsString(),
                    IsRequired = parameters.Required?.Contains(name, StringComparer.Ordinal) == true,
                    Options = schema.Enum ?? new(),
                    Min = schema.Minimum,
                    Max = schema.Maximum,
                });
            }
        }

        return manifest;
    }

    private static PowerScriptDataFormat ResolveShape(JsonSchemaObject schema, bool isInput)
    {
        if (schema.Type != "object" || schema.AdditionalProperties != false)
        {
            throw new InvalidDataException("Input and output schemas must be closed objects ('type': 'object', 'additionalProperties': false).");
        }

        var properties = schema.Properties ?? new();
        var primary = properties.Keys.Where(name => !isInput || name != "parameters").ToList();
        if (primary.Count > 1 || primary.Any(name => name is not ("files" or "text" or "image" or "audio")))
        {
            throw new InvalidDataException("Declare at most one primary property: 'files', 'text', 'image', or 'audio'; named options belong in input 'parameters'.");
        }

        if (primary.Count == 0)
        {
            return PowerScriptDataFormat.None;
        }

        var name = primary[0];
        var property = properties[name];
        if (schema.Required?.Contains(name, StringComparer.Ordinal) != true ||
            (name == "text" && property.Type != "string") ||
            (name == "files" && (property.Type != "array" || property.Items?.Type != "string")))
        {
            throw new InvalidDataException("Primary 'text' must be a required string; primary 'files' must be a required array of strings.");
        }

        if (name is "image" or "audio")
        {
            ScriptMedia.ValidateDefinition(property, name);
        }

        return ScriptIo.TryParse(name)!.Value;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
