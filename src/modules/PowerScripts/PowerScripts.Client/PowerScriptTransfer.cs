// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerScripts.Client;

/// <summary>
/// The data-plane input for <c>transform &lt;id&gt;</c>: written as JSON to the host's stdin. Every field
/// is optional — set only the ones your script consumes. Field names map to the host's snake_case
/// JSON (<c>text</c>, <c>html</c>, <c>image_path</c>, <c>file_paths</c>, <c>audio_path</c>,
/// <c>video_path</c>, <c>params</c>).
/// </summary>
public sealed class PowerScriptTransformInput
{
    /// <summary>Plain-text input (e.g. clipboard text).</summary>
    public string? Text { get; set; }

    /// <summary>HTML input.</summary>
    public string? Html { get; set; }

    /// <summary>Path to an input image.</summary>
    public string? ImagePath { get; set; }

    /// <summary>Paths to input files.</summary>
    public IReadOnlyList<string>? FilePaths { get; set; }

    /// <summary>Path to an input audio file.</summary>
    public string? AudioPath { get; set; }

    /// <summary>Path to an input video file.</summary>
    public string? VideoPath { get; set; }

    /// <summary>Extra named parameters passed to the script.</summary>
    public IReadOnlyDictionary<string, string>? Params { get; set; }
}

/// <summary>
/// The data-plane output of <c>transform &lt;id&gt;</c>: parsed from the host's stdout JSON. Only the
/// fields the script produced are populated; the rest are null.
/// </summary>
public sealed class PowerScriptTransformResult
{
    /// <summary>Plain-text output.</summary>
    public string? Text { get; init; }

    /// <summary>HTML output.</summary>
    public string? Html { get; init; }

    /// <summary>Path to a produced image.</summary>
    public string? ImagePath { get; init; }

    /// <summary>Paths to produced files.</summary>
    public IReadOnlyList<string>? FilePaths { get; init; }

    /// <summary>Path to a produced audio file.</summary>
    public string? AudioPath { get; init; }

    /// <summary>Path to a produced video file.</summary>
    public string? VideoPath { get; init; }

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
