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
/// Optional extensions provide an explicit launch recipe (<c>x-execute</c>), provenance,
/// and capability metadata (<c>x-powerscript</c>). Schemas are authoritative for I/O,
/// parameters, and schema-local file restrictions; the script extension determines the runtime.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ToolDescriptor
{
    /// <summary>Gets or sets the tool name; becomes the PowerScript <see cref="PowerScriptManifest.Id"/>.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Gets or sets optional human title; falls back to <see cref="Name"/> for the display name.</summary>
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

    /// <summary>Gets or sets how to launch the script (the one thing MCP itself does not model).</summary>
    [JsonPropertyName("x-execute")]
    public ScriptExecute? Execute { get; set; }

    /// <summary>Gets or sets powerToys-specific residue with no language-native home (see the class summary).</summary>
    [JsonPropertyName("x-powerscript")]
    public PowerScriptExtension? PowerScript { get; set; }
}
