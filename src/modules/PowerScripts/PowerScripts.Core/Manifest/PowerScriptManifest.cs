// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using PowerScripts.Core.Execution;

namespace PowerScripts.Core.Manifest;

/// <summary>
/// The parsed description of a PowerScript, built from its sibling <c>.tool.json</c> descriptor.
/// <see cref="Entry"/> names the script body file itself.
/// </summary>
public sealed class PowerScriptManifest
{
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    /// Gets or sets stable, portable identifier. Intentionally decoupled from the containing folder name so a
    /// script can be renamed on disk (or dropped into a differently-named folder to avoid a local
    /// clash) without changing its identity. Must be unique across the catalogue.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets optional .ico path relative to the script folder.</summary>
    public string? Icon { get; set; }

    /// <summary>Gets validated package-local icon path for consumers; null when absent or invalid.</summary>
    [JsonIgnore]
    public string? IconPath { get; internal set; }

    [JsonIgnore]
    public string? IconWarning { get; internal set; }

    /// <summary>Gets or sets optional author/publisher, shown in the trust prompt (e.g. "contoso" or a GitHub user).</summary>
    public string? Publisher { get; set; }

    /// <summary>Gets or sets optional semantic version of the script (e.g. "1.2.0").</summary>
    public string? Version { get; set; }

    /// <summary>Gets or sets optional provenance, e.g. the catalogue URL the script was adopted from.</summary>
    public string? Source { get; set; }

    public ScriptRuntime Runtime { get; set; } = ScriptRuntime.PowerShell;

    /// <summary>Gets or sets script body file name, relative to the script folder (e.g. "run.ps1").</summary>
    public string Entry { get; set; } = string.Empty;

    [JsonIgnore]
    public JsonSchemaObject InputSchema { get; set; } = new();

    [JsonIgnore]
    public JsonSchemaObject OutputSchema { get; set; } = new();

    /// <summary>
    /// Gets or sets file input contract; present when the script is file-driven (its input shape is
    /// <see cref="PowerScriptDataFormat.Files"/>). Carries the accepted extensions and file-count bounds.
    /// </summary>
    public ScriptInput? Input { get; set; }

    public List<ScriptParameter> Parameters { get; set; } = new();

    /// <summary>
    /// Gets or sets the primary input shape derived from inputSchema. Video remains file-backed.
    /// </summary>
    public PowerScriptDataFormat InputFormat { get; set; }

    /// <summary>Gets or sets the primary output shape derived from outputSchema.</summary>
    public PowerScriptDataFormat OutputFormat { get; set; }

    /// <summary>
    /// Gets or sets declared capabilities (e.g. "fileRead", "fileWrite", "process"). Doubles as the user-consent
    /// string and the permission contract an agent / MCP server must respect.
    /// </summary>
    public List<string> Capabilities { get; set; } = new();

    [JsonIgnore]
    public PowerScripts.Core.Cli.HostCliPermission? HostCli { get; set; }

    [JsonIgnore]
    public bool HasDescriptor { get; internal set; }

    [JsonIgnore]
    public IReadOnlyList<string> ConfigurationErrors { get; internal set; } = Array.Empty<string>();

    /// <summary>Gets or sets prototype always runs "asInvoker" (non-elevated).</summary>
    public string Elevation { get; set; } = "asInvoker";

    /// <summary>
    /// Gets or sets the launch recipe for a script authored via an explicit <c>.tool.json</c> descriptor (see
    /// <see cref="ScriptExecute"/>). When set, the executor runs the script through this recipe
    /// (interpreter + argv / stdin) instead of the built-in PowerShell or Python runtime.
    /// </summary>
    [JsonIgnore]
    public ScriptExecute? Execute { get; set; }

    /// <summary>Gets or sets absolute path to the folder that contains this manifest. Populated by the registry.</summary>
    [JsonIgnore]
    public string FolderPath { get; set; } = string.Empty;

    /// <summary>Gets or sets the configured catalogue root containing this package. Populated by the registry.</summary>
    [JsonIgnore]
    public string ScriptsRoot { get; set; } = string.Empty;

    /// <summary>Gets absolute path to the script body file.</summary>
    [JsonIgnore]
    public string EntryFullPath => string.IsNullOrEmpty(FolderPath) ? Entry : Path.Combine(FolderPath, Entry);
}
