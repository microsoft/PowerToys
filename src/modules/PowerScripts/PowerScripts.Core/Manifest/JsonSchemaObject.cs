// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace PowerScripts.Core.Manifest;

/// <summary>
/// A minimal JSON Schema <c>object</c> node: enough to read a tool's declared inputs/outputs without
/// pulling in a full JSON Schema library.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class JsonSchemaObject
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("properties")]
    public Dictionary<string, JsonSchemaProperty>? Properties { get; set; }

    [JsonPropertyName("required")]
    public List<string>? Required { get; set; }

    [JsonPropertyName("additionalProperties")]
    public bool? AdditionalProperties { get; set; }

    [JsonPropertyName("items")]
    public JsonSchemaProperty? Items { get; set; }

    [JsonPropertyName("minItems")]
    public int? MinItems { get; set; }

    [JsonPropertyName("maxItems")]
    public int? MaxItems { get; set; }

    [JsonPropertyName("x-powerscript")]
    public FileSchemaHints? PowerScript { get; set; }
}
