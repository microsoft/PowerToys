// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerScripts.Client;

/// <summary>
/// The data-plane input for <c>transform &lt;id&gt;</c>: written as JSON to the host's stdin. Every field
/// is optional — set only the ones your script consumes. Fields map to the schema's
/// JSON (<c>text</c>, <c>files</c>, <c>parameters</c>). Inline HTML uses text; media and HTML
/// documents use file paths, matched by extension and selection count regardless of their source.
/// </summary>
public sealed class PowerScriptTransformInput
{
    /// <summary>String input, including plain text or inline HTML.</summary>
    public string? Text { get; set; }

    /// <summary>Paths to input files.</summary>
    public IReadOnlyList<string>? FilePaths { get; set; }

    /// <summary>Extra named parameters passed to the script.</summary>
    public IReadOnlyDictionary<string, System.Text.Json.JsonElement>? Parameters { get; set; }
}

/// <summary>
/// The data-plane output of <c>transform &lt;id&gt;</c>: parsed from the host's stdout JSON. Only the
/// fields the script produced are populated; the rest are null.
/// </summary>
public sealed class PowerScriptTransformResult
{
    /// <summary>Plain-text output.</summary>
    public string? Text { get; init; }

    /// <summary>Paths to produced files, including HTML documents and media. A single file is still a list.</summary>
    public IReadOnlyList<string>? FilePaths { get; init; }

    /// <summary>The host process exit code (see <see cref="PowerScriptsProtocol.ExitCode"/>).</summary>
    public int ExitCode { get; init; }

    /// <summary>Anything the script wrote to stderr (diagnostics).</summary>
    public string StdErr { get; init; } = string.Empty;

    /// <summary>True when the transform completed successfully.</summary>
    public bool Succeeded => ExitCode == PowerScriptsProtocol.ExitCode.Ok;
}

/// <summary>The outcome of <c>run &lt;id&gt;</c>.</summary>
public sealed class PowerScriptRunResult
{
    /// <summary>The host process exit code (see <see cref="PowerScriptsProtocol.ExitCode"/>).</summary>
    public int ExitCode { get; init; }

    /// <summary>Captured standard output.</summary>
    public string StdOut { get; init; } = string.Empty;

    /// <summary>Captured standard error.</summary>
    public string StdErr { get; init; } = string.Empty;

    /// <summary>True when the run completed successfully.</summary>
    public bool Succeeded => ExitCode == PowerScriptsProtocol.ExitCode.Ok;
}
