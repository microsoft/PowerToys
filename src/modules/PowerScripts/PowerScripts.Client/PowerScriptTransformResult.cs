// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerScripts.Client;

/// <summary>
/// The data-plane output of <c>transform &lt;id&gt;</c>: parsed from the host's stdout JSON. Only the
/// fields the script produced are populated; the rest are null.
/// </summary>
public sealed class PowerScriptTransformResult
{
    /// <summary>Gets plain-text output.</summary>
    public string? Text { get; init; }

    /// <summary>Gets paths to produced files, including HTML documents and media. A single file is still a list.</summary>
    public IReadOnlyList<string>? FilePaths { get; init; }

    /// <summary>Gets returned inline images.</summary>
    public PowerScriptMedia? Image { get; init; }

    /// <summary>Gets returned inline audio.</summary>
    public PowerScriptMedia? Audio { get; init; }

    /// <summary>Gets the host process exit code (see <see cref="PowerScriptsProtocol.ExitCode"/>).</summary>
    public int ExitCode { get; init; }

    /// <summary>Gets anything the script wrote to stderr (diagnostics).</summary>
    public string StdErr { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether true when the transform completed successfully.</summary>
    public bool Succeeded => ExitCode == PowerScriptsProtocol.ExitCode.Ok;
}
