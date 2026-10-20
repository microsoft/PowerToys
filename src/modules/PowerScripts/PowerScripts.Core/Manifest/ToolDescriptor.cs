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
/// Only one key is not part of MCP: <see cref="Execute"/> (<c>x-execute</c>) says how to turn the
/// JSON inputs into a command line. It is an <c>x-</c>-prefixed extension that any MCP / JSON Schema
/// reader ignores, so the file stays a valid MCP tool. A second optional extension,
/// <see cref="PowerScript"/> (<c>x-powerscript</c>), carries the small residue that has no
/// language-native home (the Explorer file-type filter and capability/consent string) — the metadata
/// that introspection could never recover on its own.
/// </summary>
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
public sealed class JsonSchemaObject
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("properties")]
    public Dictionary<string, JsonSchemaProperty>? Properties { get; set; }

    [JsonPropertyName("required")]
    public List<string>? Required { get; set; }
}

/// <summary>A single property of a JSON Schema object.</summary>
public sealed class JsonSchemaProperty
{
    public const string FilePathFormat = "file-path";

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("format")]
    public string? Format { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("default")]
    public JsonElement? Default { get; set; }

    [JsonPropertyName("enum")]
    public List<string>? Enum { get; set; }

    [JsonPropertyName("minimum")]
    public int? Minimum { get; set; }

    [JsonPropertyName("maximum")]
    public int? Maximum { get; set; }

    /// <summary>
    /// When set on a property without <c>format: file-path</c>, this string value is a
    /// <em>path to a file</em> of the given media type (never raw bytes). Its presence marks the
    /// property as the script's primary file input rather than a prompt parameter.
    /// </summary>
    [JsonPropertyName("contentMediaType")]
    public string? ContentMediaType { get; set; }

    /// <summary>True when this is a scalar file-path parameter rendered with a consumer-owned picker.</summary>
    [JsonIgnore]
    public bool IsFileParameter => string.Equals(Format, FilePathFormat, StringComparison.OrdinalIgnoreCase);

    /// <summary>True when this property declares the script's primary Explorer file input.</summary>
    [JsonIgnore]
    public bool IsPrimaryFileInput => !IsFileParameter && !string.IsNullOrEmpty(ContentMediaType);

    /// <summary>Renders the schema <c>default</c> (of any JSON type) as the string the manifest uses.</summary>
    public string? DefaultAsString()
    {
        if (Default is not { } value)
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => value.GetRawText(),
        };
    }
}

/// <summary>
/// The <c>x-execute</c> block: how the launcher turns JSON inputs into a process. This is the only
/// part of the descriptor MCP does not define, and the only part <em>PowerScripts</em>, not an agent,
/// reads.
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
public sealed class PowerScriptExtension
{
    /// <summary>Overrides the runtime instead of inferring it from the entry extension.</summary>
    [JsonPropertyName("runtime")]
    public string? Runtime { get; set; }

    /// <summary>
    /// Optional explicit Python entry-function name. When set, the runner calls this function instead
    /// of discovering it by the <c>powerscript_from_&lt;in&gt;_to_&lt;out&gt;</c> naming convention, so the
    /// author can name the function anything and declare its <see cref="Input"/>/<see cref="Output"/>
    /// here. Ignored for non-Python runtimes. Omitted keeps the zero-config convention.
    /// </summary>
    [JsonPropertyName("function")]
    public string? Function { get; set; }

    /// <summary>Explorer right-click file-type filter for a file script (e.g. <c>[".md", ".txt"]</c> or <c>["*"]</c>).</summary>
    [JsonPropertyName("extensions")]
    public List<string>? Extensions { get; set; }

    [JsonPropertyName("minFiles")]
    public int? MinFiles { get; set; }

    [JsonPropertyName("maxFiles")]
    public int? MaxFiles { get; set; }

    /// <summary>Declared capabilities: the consent string and the agent permission contract.</summary>
    [JsonPropertyName("capabilities")]
    public List<string>? Capabilities { get; set; }

    /// <summary>
    /// Optional explicit input data shape (<c>none</c>/<c>text</c>/<c>html</c>/<c>image</c>/<c>audio</c>/
    /// <c>video</c>/<c>files</c>). When omitted it is inferred: from a Python
    /// <c>powerscript_from_*_to_*</c> function, or from a declared file input, otherwise none. Authors
    /// declare only I/O — never consuming modules. A resolved input of <c>files</c> is what makes a
    /// script file-driven (surfaced in the Explorer right-click menu); there is no separate "kind".
    /// </summary>
    [JsonPropertyName("input")]
    public string? Input { get; set; }

    /// <summary>Optional explicit output data shape; inferred when omitted (see <see cref="Input"/>).</summary>
    [JsonPropertyName("output")]
    public string? Output { get; set; }

    [JsonPropertyName("publisher")]
    public string? Publisher { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    /// <summary>Display-only MXC recommendations. They cannot disable an enforced policy.</summary>
    [JsonPropertyName("mxc")]
    public PowerScriptMxcExtension? Mxc { get; set; }
}

public sealed class PowerScriptMxcExtension
{
    [JsonPropertyName("recommendedPolicies")]
    public List<string>? RecommendedPolicies { get; set; }
}
