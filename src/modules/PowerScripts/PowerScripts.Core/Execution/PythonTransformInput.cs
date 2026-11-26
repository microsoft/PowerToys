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

/// <summary>The typed input handed to a Python PowerScript transform: text, file paths, and parameters.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class PythonTransformInput
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("files")]
    public List<string>? FilePaths { get; set; }

    [JsonPropertyName("image")]
    public JsonElement? Image { get; set; }

    [JsonPropertyName("audio")]
    public JsonElement? Audio { get; set; }

    [JsonPropertyName("parameters")]
    public Dictionary<string, string>? Params { get; set; }

    public JsonObject CreateRequest(PowerScriptManifest manifest, bool validate = true)
    {
        if ((manifest.InputFormat != PowerScriptDataFormat.Files && FilePaths is { Count: > 0 }) ||
            (manifest.InputFormat != PowerScriptDataFormat.Text && Text is not null) ||
            (manifest.InputFormat != PowerScriptDataFormat.Image && Image is not null) ||
            (manifest.InputFormat != PowerScriptDataFormat.Audio && Audio is not null))
        {
            throw new InvalidDataException("The supplied primary data does not match inputSchema.");
        }

        var request = new JsonObject();
        if (manifest.InputFormat == PowerScriptDataFormat.Files)
        {
            request["files"] = JsonSerializer.SerializeToNode(FilePaths ?? new());
        }
        else if (manifest.InputFormat == PowerScriptDataFormat.Text)
        {
            request["text"] = Text ?? string.Empty;
        }
        else if (manifest.InputFormat is PowerScriptDataFormat.Image or PowerScriptDataFormat.Audio)
        {
            var value = manifest.InputFormat == PowerScriptDataFormat.Image ? Image : Audio;
            if (value is { } media)
            {
                request[ScriptIo.ToToken(manifest.InputFormat)] = JsonNode.Parse(media.GetRawText());
            }
        }

        if (manifest.InputSchema.Properties?.ContainsKey("parameters") == true)
        {
            var parameters = new JsonObject();
            foreach (var (name, value) in Params ?? new())
            {
                var declaration = manifest.Parameters.SingleOrDefault(parameter => parameter.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidDataException($"Unknown parameter '{name}'.");
                parameters[declaration.Name] = declaration.IsBool
                    ? JsonValue.Create(bool.Parse(value))
                    : declaration.IsInt
                        ? JsonValue.Create(int.Parse(value, CultureInfo.InvariantCulture))
                        : JsonValue.Create(value);
            }

            request["parameters"] = parameters;
        }
        else if (Params is { Count: > 0 })
        {
            throw new InvalidDataException("This script does not declare parameters.");
        }

        ScriptSchema.ApplyDefaults(manifest.InputSchema, request);
        if (validate)
        {
            ScriptSchema.Validate(manifest.InputSchema, JsonSerializer.SerializeToElement(request), "input");
        }

        return request;
    }

    public static PythonTransformInput Parse(PowerScriptManifest manifest, string json)
    {
        if (manifest.InputFormat is PowerScriptDataFormat.Image or PowerScriptDataFormat.Audio && json.Length > ScriptMedia.MaxRequestCharacters)
        {
            throw new InvalidDataException("Inline media request exceeds the bounded payload size.");
        }

        var request = JsonNode.Parse(json) as JsonObject
            ?? throw new InvalidDataException("Input must be an object.");
        if (manifest.InputSchema.Properties?.ContainsKey("parameters") == true && !request.ContainsKey("parameters"))
        {
            request["parameters"] = new JsonObject();
        }

        ScriptSchema.ApplyDefaults(manifest.InputSchema, request);
        var value = JsonSerializer.SerializeToElement(request);
        ScriptSchema.Validate(manifest.InputSchema, value, "input");
        return new PythonTransformInput
        {
            Text = request["text"]?.GetValue<string>(),
            FilePaths = request["files"]?.Deserialize<List<string>>(),
            Image = request["image"]?.Deserialize<JsonElement>(),
            Audio = request["audio"]?.Deserialize<JsonElement>(),
            Params = request["parameters"]?.AsObject().ToDictionary(
                pair => pair.Key,
                pair => pair.Value is JsonValue scalar && scalar.TryGetValue<string>(out var text) ? text : pair.Value!.ToJsonString(),
                StringComparer.Ordinal),
        };
    }
}
