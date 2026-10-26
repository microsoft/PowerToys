// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace PowerScripts.Core.Manifest;

/// <summary>
/// An explicit, hand-authored PowerScript descriptor in the shape of an <b>MCP <c>Tool</c></b>. A
/// descriptor lives in a sidecar file named <c>&lt;script&gt;.tool.json</c> next to the script it
/// describes (e.g. <c>narrate_image.py.tool.json</c> beside <c>narrate_image.py</c>), so the same
/// contract that an AI agent already understands (<c>name</c> + <c>description</c> +
/// <c>inputSchema</c> + <c>outputSchema</c>) doubles as PowerScripts' authoring format — with no new
/// protocol to invent and no server to run.
///
/// Optional extensions provide an explicit launch recipe (<c>x-execute</c>) and discovery,
/// provenance, and capability metadata (<c>x-powerscript</c>). Schemas are authoritative for I/O,
/// parameters, and schema-local file restrictions; the script extension determines the runtime.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ToolDescriptor
{
    /// <summary>The tool name; becomes the PowerScript <see cref="PowerScriptManifest.Id"/>.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Optional human title; falls back to <see cref="Name"/> for the display name.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("inputSchema")]
    public JsonSchemaObject? InputSchema { get; set; }

    [JsonPropertyName("outputSchema")]
    public JsonSchemaObject? OutputSchema { get; set; }

    [JsonPropertyName("annotations")]
    public ToolAnnotations? Annotations { get; set; }

    /// <summary>How to launch the script (the one thing MCP itself does not model).</summary>
    [JsonPropertyName("x-execute")]
    public ScriptExecute? Execute { get; set; }

    /// <summary>PowerToys-specific residue with no language-native home (see the class summary).</summary>
    [JsonPropertyName("x-powerscript")]
    public PowerScriptExtension? PowerScript { get; set; }
}

/// <summary>The subset of MCP tool annotations PowerScripts reads.</summary>
public sealed class ToolAnnotations
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("readOnlyHint")]
    public bool? ReadOnlyHint { get; set; }

    [JsonPropertyName("destructiveHint")]
    public bool? DestructiveHint { get; set; }
}

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

/// <summary>A single property of a JSON Schema object.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class JsonSchemaProperty : JsonSchemaObject
{
    public const string FilePathFormat = "file-path";

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

    /// <summary>True when this is a scalar file-path parameter rendered with a consumer-owned picker.</summary>
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

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class FileSchemaHints
{
    [JsonPropertyName("extensions")]
    public List<string>? Extensions { get; set; }
}

/// <summary>
/// The optional <c>x-execute</c> extension describes a custom executable and input transport.
/// </summary>
public sealed class ScriptExecute
{
    /// <summary>
    /// The command and its fixed leading arguments, e.g. <c>["python3", "narrate_image.py"]</c>. The
    /// first token is the interpreter; a token that names the sibling script is resolved to its full
    /// path at run time.
    /// </summary>
    [JsonPropertyName("command")]
    public List<string>? Command { get; set; }

    /// <summary>
    /// Maps an input property name to the command-line flag that carries it (e.g.
    /// <c>{"voice": "--voice"}</c>). Names absent from the map are passed as <c>--&lt;name&gt;</c>.
    /// </summary>
    [JsonPropertyName("argMap")]
    public Dictionary<string, string>? ArgMap { get; set; }

    /// <summary>
    /// When <c>"json"</c>, the whole input object is written to the process' stdin as a single JSON
    /// document instead of being spread across argv — the channel for large / structured inputs.
    /// </summary>
    [JsonPropertyName("stdin")]
    public string? Stdin { get; set; }

    /// <summary>Optional human meaning of non-zero exit codes, surfaced in errors.</summary>
    [JsonPropertyName("exitCodes")]
    public Dictionary<string, string>? ExitCodes { get; set; }

    /// <summary>True when the structured input should be delivered on stdin as JSON.</summary>
    [JsonIgnore]
    public bool UsesJsonStdin => string.Equals(Stdin, "json", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The <c>x-powerscript</c> block: the small, PowerToys-specific residue that neither MCP nor a
/// language signature can express. Everything here is optional.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class PowerScriptExtension
{
    /// <summary>Declared capabilities: the consent string and the agent permission contract.</summary>
    [JsonPropertyName("capabilities")]
    public List<string>? Capabilities { get; set; }

    /// <summary>Optional discovery audience. Omitted means every compatible consumer; not execution authorization.</summary>
    [JsonPropertyName("consumers")]
    public List<string>? Consumers { get; set; }

    [JsonPropertyName("publisher")]
    public string? Publisher { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

}
