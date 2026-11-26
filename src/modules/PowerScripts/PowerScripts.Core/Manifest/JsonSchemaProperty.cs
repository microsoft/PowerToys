// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace PowerScripts.Core.Manifest;

/// <summary>A single property of a JSON Schema object.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class JsonSchemaProperty : JsonSchemaObject
{
    public const string FilePathFormat = "file-path";

    [JsonPropertyName("x-ui")]
    public ParameterUiHints? Ui { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("format")]
    public string? Format { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("default")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public JsonElement Default { get; set; }

    [JsonPropertyName("enum")]
    public List<string>? Enum { get; set; }

    [JsonPropertyName("minimum")]
    public int? Minimum { get; set; }

    [JsonPropertyName("maximum")]
    public int? Maximum { get; set; }

    [JsonPropertyName("minLength")]
    public int? MinLength { get; set; }

    [JsonPropertyName("maxLength")]
    public int? MaxLength { get; set; }

    [JsonPropertyName("pattern")]
    public string? Pattern { get; set; }

    [JsonPropertyName("contentEncoding")]
    public string? ContentEncoding { get; set; }

    [JsonPropertyName("contentMediaType")]
    public string? ContentMediaType { get; set; }

    /// <summary>Gets a value indicating whether true when this is a scalar file-path parameter rendered with a consumer-owned picker.</summary>
    [JsonIgnore]
    public bool IsFileParameter => string.Equals(Format, FilePathFormat, StringComparison.OrdinalIgnoreCase);

    /// <summary>Renders the schema <c>default</c> (of any JSON type) as the string the manifest uses.</summary>
    public string? DefaultAsString()
    {
        if (Default.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        return Default.ValueKind switch
        {
            JsonValueKind.String => Default.GetString(),
            JsonValueKind.Number => Default.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => Default.GetRawText(),
        };
    }
}
