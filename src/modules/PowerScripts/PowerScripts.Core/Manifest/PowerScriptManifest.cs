// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using PowerScripts.Core.Execution;
using PowerScripts.Core.Security;

namespace PowerScripts.Core.Manifest;

/// <summary>
/// The runtime used to execute a PowerScript. PowerShell and Python are supported; the enum exists
/// so further runtimes (e.g. Node) can be added without a schema break.
/// </summary>
public enum ScriptRuntime
{
    PowerShell,

    /// <summary>
    /// A Python script whose entry file exposes a single
    /// <c>powerscript_from_&lt;input&gt;_to_&lt;output&gt;</c> function. Runs on native Windows Python or
    /// inside WSL depending on the user's PowerScripts Python settings.
    /// </summary>
    Python,
}

/// <summary>
/// The kind of result a file PowerScript produces.
/// </summary>
public enum ScriptOutputType
{
    None,

    /// <summary>Produces a converted file (e.g. HEIC -> JPG).</summary>
    ConvertedFile,

    /// <summary>Performs a side effect (e.g. checksum, OCR, strip metadata).</summary>
    SideEffect,
}

/// <summary>
/// Declares the file input contract for a file-driven script (one whose input shape is
/// <see cref="PowerScriptDataFormat.Files"/>).
/// </summary>
public sealed class ScriptInput
{
    /// <summary>File extensions this script accepts (e.g. ".heic"). "*" means any extension.</summary>
    public List<string> Extensions { get; set; } = new();

    /// <summary>Minimum number of files required.</summary>
    public int MinFiles { get; set; } = 1;

    /// <summary>Maximum number of files; 0 means unbounded.</summary>
    public int MaxFiles { get; set; }
}

/// <summary>
/// Declares the output contract for a file-driven script.
/// </summary>
public sealed class ScriptOutput
{
    public ScriptOutputType Type { get; set; } = ScriptOutputType.None;

    /// <summary>For <see cref="ScriptOutputType.ConvertedFile"/>: the produced extension (e.g. ".jpg").</summary>
    public string? Extension { get; set; }
}

/// <summary>
/// Display-only MXC guidance supplied by a script author. Recommendations never weaken the
/// user/system policy selected by <see cref="MxcPolicyResolver"/>.
/// </summary>
public sealed class PowerScriptMxcMetadata
{
    public List<string> RecommendedPolicies { get; set; } = MxcPolicies.All.ToList();
}

/// <summary>
/// A typed parameter contract exposed to consumer modules. Each consumer owns its parameter UI and
/// passes chosen values to the Host. Values reach scripts as strings.
/// </summary>
public sealed class ScriptParameter
{
    public const string ParameterTypeString = "string";
    public const string ParameterTypeInt = "int";
    public const string ParameterTypeBool = "bool";
    public const string ParameterTypeChoice = "choice";
    public const string ParameterTypeFile = "file";

    public string Name { get; set; } = string.Empty;

    /// <summary>One of: "string", "int", "bool", "choice", "file".</summary>
    public string Type { get; set; } = ParameterTypeString;

    /// <summary>Optional consumer-facing display label; falls back to <see cref="Name"/>.</summary>
    public string? Label { get; set; }

    /// <summary>Optional consumer-facing help text.</summary>
    public string? Description { get; set; }

    public string? Default { get; set; }

    public bool IsRequired { get; set; }

    /// <summary>Allowed values for a <see cref="ParameterTypeChoice"/> parameter.</summary>
    public List<string> Options { get; set; } = new();

    public int? Min { get; set; }

    public int? Max { get; set; }

    /// <summary>The display label to use in UI (label if set, otherwise the name).</summary>
    [JsonIgnore]
    public string DisplayLabel => string.IsNullOrWhiteSpace(Label) ? Name : Label!;

    /// <summary>True when <see cref="Type"/> is the choice type (case-insensitive).</summary>
    [JsonIgnore]
    public bool IsChoice => string.Equals(Type, ParameterTypeChoice, StringComparison.OrdinalIgnoreCase);

    /// <summary>True when <see cref="Type"/> is the bool type (case-insensitive).</summary>
    [JsonIgnore]
    public bool IsBool => string.Equals(Type, ParameterTypeBool, StringComparison.OrdinalIgnoreCase);

    /// <summary>True when <see cref="Type"/> is the int type (case-insensitive).</summary>
    [JsonIgnore]
    public bool IsInt => string.Equals(Type, ParameterTypeInt, StringComparison.OrdinalIgnoreCase);

    /// <summary>True when <see cref="Type"/> is the file type (case-insensitive).</summary>
    [JsonIgnore]
    public bool IsFile => string.Equals(Type, ParameterTypeFile, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The parsed description of a single PowerScript. It is built from the script file's leading
/// <c>@powerscript.*</c> comment header (see <see cref="ScriptHeaderParser"/>); <see cref="Entry"/>
/// names the script body file itself.
/// </summary>
public sealed class PowerScriptManifest
{
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    /// Stable, portable identifier. Intentionally decoupled from the containing folder name so a
    /// script can be renamed on disk (or dropped into a differently-named folder to avoid a local
    /// clash) without changing its identity. Must be unique across the catalogue.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Optional icon file name, relative to the script folder.</summary>
    public string? Icon { get; set; }

    /// <summary>Optional author/publisher, shown in the trust prompt (e.g. "contoso" or a GitHub user).</summary>
    public string? Publisher { get; set; }

    /// <summary>Optional semantic version of the script (e.g. "1.2.0").</summary>
    public string? Version { get; set; }

    /// <summary>Optional provenance, e.g. the catalogue URL the script was adopted from.</summary>
    public string? Source { get; set; }

    public ScriptRuntime Runtime { get; set; } = ScriptRuntime.PowerShell;

    /// <summary>Script body file name, relative to the script folder (e.g. "run.ps1").</summary>
    public string Entry { get; set; } = string.Empty;

    /// <summary>
    /// Optional explicit entry-function name for a Python script. When set, the Python runner calls
    /// this function instead of discovering it by the <c>powerscript_from_*_to_*</c> naming convention,
    /// so authors can name the function anything and declare its I/O in the descriptor instead. Null
    /// keeps the zero-config convention (the function name carries the I/O contract).
    /// </summary>
    public string? EntryFunction { get; set; }

    /// <summary>
    /// File input contract; present when the script is file-driven (its input shape is
    /// <see cref="PowerScriptDataFormat.Files"/>). Carries the accepted extensions and file-count bounds.
    /// </summary>
    public ScriptInput? Input { get; set; }

    public ScriptOutput? Output { get; set; }

    public List<ScriptParameter> Parameters { get; set; } = new();

    /// <summary>
    /// The data shape this script consumes, when the author declares it explicitly (e.g. a PowerShell
    /// script that reads text and writes html). Null means "infer": from the Python
    /// <c>powerscript_from_*_to_*</c> function, or from a declared file <see cref="Input"/> (files),
    /// otherwise none. Populated (baked) by the registry once resolved so every consumer reads the same
    /// shape. A script declares only its own I/O — never which modules consume it — so adding a new
    /// consuming module never requires touching any script.
    /// </summary>
    public PowerScriptDataFormat? InputFormat { get; set; }

    /// <summary>The data shape this script produces, when declared explicitly; null means infer.</summary>
    public PowerScriptDataFormat? OutputFormat { get; set; }

    /// <summary>
    /// Declared capabilities (e.g. "fileRead", "fileWrite", "process"). Doubles as the user-consent
    /// string and the permission contract an agent / MCP server must respect.
    /// </summary>
    public List<string> Capabilities { get; set; } = new();

    /// <summary>Display-only MXC recommendations; enforcement remains centrally resolved.</summary>
    public PowerScriptMxcMetadata Mxc { get; set; } = new();

    /// <summary>Prototype always runs "asInvoker" (non-elevated).</summary>
    public string Elevation { get; set; } = "asInvoker";

    /// <summary>
    /// The launch recipe for a script authored via an explicit <c>.tool.json</c> descriptor (see
    /// <see cref="ScriptExecute"/>). When set, the executor runs the script through this recipe
    /// (interpreter + argv / stdin) instead of the built-in PowerShell / Python convention path.
    /// Null for header-authored scripts, which keep the legacy execution behavior unchanged.
    /// </summary>
    [JsonIgnore]
    public ScriptExecute? Execute { get; set; }

    /// <summary>Absolute path to the folder that contains this manifest. Populated by the registry.</summary>
    [JsonIgnore]
    public string FolderPath { get; set; } = string.Empty;

    /// <summary>The configured catalogue root containing this package. Populated by the registry.</summary>
    [JsonIgnore]
    public string ScriptsRoot { get; set; } = string.Empty;

    /// <summary>Absolute path to the script body file.</summary>
    [JsonIgnore]
    public string EntryFullPath => string.IsNullOrEmpty(FolderPath) ? Entry : Path.Combine(FolderPath, Entry);
}
