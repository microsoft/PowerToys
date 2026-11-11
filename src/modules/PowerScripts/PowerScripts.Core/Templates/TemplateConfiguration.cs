// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;
using PowerScripts.Core.Manifest;

namespace PowerScripts.Core.Templates;

/// <summary>Reads the bounded template request without silently replacing duplicate configuration keys.</summary>
public static class TemplateConfiguration
{
    public const int MaximumLength = 16_384;

    public static ScriptTemplateOptions Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (json.Length > MaximumLength)
        {
            throw new InvalidDataException("Template configuration is too large.");
        }

        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Template configuration must be a JSON object.");
        }

        ValidatePropertyNames(document.RootElement);
        var options = new JsonSerializerOptions(ToolDescriptorParser.JsonOptions)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
        return document.RootElement.Deserialize<ScriptTemplateOptions>(options)
            ?? throw new InvalidDataException("Template configuration must be a JSON object.");
    }

    private static void ValidatePropertyNames(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new InvalidDataException($"Template configuration contains a duplicate property: '{property.Name}'.");
                }

                ValidatePropertyNames(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                ValidatePropertyNames(item);
            }
        }
    }
}
