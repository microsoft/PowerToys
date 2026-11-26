// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using PowerScripts.Core.Manifest;
using PowerScripts.Core.Security;

namespace PowerScripts.Core.Execution;

/// <summary>The typed result produced by a Python PowerScript transform.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class PythonTransformResult
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("files")]
    public List<string>? FilePaths { get; set; }

    [JsonPropertyName("image")]
    public JsonElement? Image { get; set; }

    [JsonPropertyName("audio")]
    public JsonElement? Audio { get; set; }

    [JsonIgnore]
    public int ExitCode { get; set; }

    [JsonIgnore]
    public string StdErr { get; set; } = string.Empty;

    [JsonIgnore]
    public bool Succeeded => ExitCode == 0;

    /// <summary>Reads an output object and checks that it matches the descriptor, without guessing paths from text.</summary>
    public static PythonTransformResult Parse(string json, PowerScriptDataFormat format, JsonSchemaObject? schema = null)
    {
        if (format is PowerScriptDataFormat.Image or PowerScriptDataFormat.Audio && json.Length > ScriptMedia.MaxRequestCharacters)
        {
            throw new InvalidDataException("Inline media result exceeds the bounded payload size.");
        }

        if (schema is not null)
        {
            using var document = JsonDocument.Parse(json);
            ScriptSchema.Validate(schema, document.RootElement, "output");
        }

        var result = JsonSerializer.Deserialize<PythonTransformResult>(json)
            ?? throw new JsonException("Expected an output object.");
        var populated = (result.Text is not null ? 1 : 0) + (result.FilePaths is not null ? 1 : 0) +
            (result.Image is not null ? 1 : 0) + (result.Audio is not null ? 1 : 0);
        var valid = format switch
        {
            PowerScriptDataFormat.None => populated == 0,
            PowerScriptDataFormat.Text => populated == 1 && result.Text is not null,
            PowerScriptDataFormat.Image => populated == 1 && result.Image is { ValueKind: JsonValueKind.Object or JsonValueKind.Array },
            PowerScriptDataFormat.Audio => populated == 1 && result.Audio is { ValueKind: JsonValueKind.Object or JsonValueKind.Array },
            PowerScriptDataFormat.Files => populated == 1 && result.FilePaths is not null &&
                result.FilePaths.All(path => !string.IsNullOrWhiteSpace(path)),
            _ => false,
        };
        if (!valid)
        {
            throw new JsonException($"Output does not match '{ScriptIo.ToToken(format)}'. Return one declared primary field: text, files, image, or audio; return {{}} for none.");
        }

        return result;
    }
}
