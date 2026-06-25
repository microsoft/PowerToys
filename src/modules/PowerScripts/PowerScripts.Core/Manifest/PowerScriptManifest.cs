// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace PowerScripts.Core.Manifest;

/// <summary>
/// What a PowerScript operates on.
/// </summary>
public enum ScriptKind
{
    /// <summary>Acts on the PC; no file input. Surfaced via hotkey / Command Palette.</summary>
    System,

    /// <summary>Acts on one or more input files of a declared type. Surfaced in the right-click menu.</summary>
    File,
}

/// <summary>
/// The runtime used to execute a PowerScript. Only PowerShell is supported in the prototype;
/// the field exists so Python / Node can be added without a schema break.
/// </summary>
public enum ScriptRuntime
{
    PowerShell,
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
/// Declares the file input contract for a <see cref="ScriptKind.File"/> script.
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
/// Declares the output contract for a <see cref="ScriptKind.File"/> script.
/// </summary>
public sealed class ScriptOutput
{
    public ScriptOutputType Type { get; set; } = ScriptOutputType.None;

    /// <summary>For <see cref="ScriptOutputType.ConvertedFile"/>: the produced extension (e.g. ".jpg").</summary>
    public string? Extension { get; set; }
}

/// <summary>
/// A typed, user-editable parameter passed to the script.
/// </summary>
public sealed class ScriptParameter
{
    public string Name { get; set; } = string.Empty;

    /// <summary>One of: "string", "int", "bool".</summary>
    public string Type { get; set; } = "string";

    public string? Default { get; set; }

    public int? Min { get; set; }

    public int? Max { get; set; }
}

/// <summary>
/// The on-disk description of a single PowerScript. One script lives in its own folder containing
/// a <c>manifest.json</c> (this type) plus the script body referenced by <see cref="Entry"/>.
/// </summary>
public sealed class PowerScriptManifest
{
    public int SchemaVersion { get; set; } = 1;

    /// <summary>Stable identifier; must match the containing folder name.</summary>
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Optional icon file name, relative to the script folder.</summary>
    public string? Icon { get; set; }

    public ScriptKind Kind { get; set; }

    public ScriptRuntime Runtime { get; set; } = ScriptRuntime.PowerShell;

    /// <summary>Script body file name, relative to the script folder (e.g. "run.ps1").</summary>
    public string Entry { get; set; } = string.Empty;

    /// <summary>File input contract; required for <see cref="ScriptKind.File"/>.</summary>
    public ScriptInput? Input { get; set; }

    public ScriptOutput? Output { get; set; }

    public List<ScriptParameter> Parameters { get; set; } = new();

    /// <summary>Where the script appears, e.g. "contextMenu", "keyboardManager", "commandPalette".</summary>
    public List<string> Surfaces { get; set; } = new();

    /// <summary>
    /// Declared capabilities (e.g. "fileRead", "fileWrite", "process"). Doubles as the user-consent
    /// string and the permission contract an agent / MCP server must respect.
    /// </summary>
    public List<string> Capabilities { get; set; } = new();

    /// <summary>Prototype always runs "asInvoker" (non-elevated).</summary>
    public string Elevation { get; set; } = "asInvoker";

    /// <summary>Absolute path to the folder that contains this manifest. Populated by the registry.</summary>
    [JsonIgnore]
    public string FolderPath { get; set; } = string.Empty;

    /// <summary>Absolute path to the script body file.</summary>
    [JsonIgnore]
    public string EntryFullPath => string.IsNullOrEmpty(FolderPath) ? Entry : Path.Combine(FolderPath, Entry);

    /// <summary>True if this script declares the given surface (case-insensitive).</summary>
    public bool HasSurface(string surface) =>
        Surfaces.Any(s => string.Equals(s, surface, StringComparison.OrdinalIgnoreCase));
}
