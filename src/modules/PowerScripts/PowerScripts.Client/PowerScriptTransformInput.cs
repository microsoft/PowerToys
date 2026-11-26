// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerScripts.Client;

/// <summary>
/// The data-plane input for <c>transform &lt;id&gt;</c>: written as JSON to the host's stdin. Every field
/// is optional — set only the ones your script consumes. Fields map to the schema's
/// JSON (<c>text</c>, <c>files</c>, <c>image</c>, <c>audio</c>, <c>parameters</c>).
/// Video and large assets remain file-backed.
/// </summary>
public sealed class PowerScriptTransformInput
{
    /// <summary>Gets or sets string input, including plain text or inline HTML.</summary>
    public string? Text { get; set; }

    /// <summary>Gets or sets paths to input files.</summary>
    public IReadOnlyList<string>? FilePaths { get; set; }

    /// <summary>Gets or sets inline image bytes with MIME types; a scalar or explicit collection.</summary>
    public PowerScriptMedia? Image { get; set; }

    /// <summary>Gets or sets inline audio bytes with MIME types; a scalar or explicit collection.</summary>
    public PowerScriptMedia? Audio { get; set; }

    /// <summary>Gets or sets extra named parameters passed to the script.</summary>
    public IReadOnlyDictionary<string, System.Text.Json.JsonElement>? Parameters { get; set; }
}
