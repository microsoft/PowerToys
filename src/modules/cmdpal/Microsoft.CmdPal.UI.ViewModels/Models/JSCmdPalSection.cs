// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

/// <summary>
/// Represents the "cmdpal" section of a package.json, carrying the
/// CmdPal-specific metadata for a JavaScript/TypeScript extension.
/// </summary>
public sealed record JSCmdPalSection
{
    /// <summary>
    /// Gets the human-readable name shown in the CmdPal UI. Falls back to the package name when absent.
    /// </summary>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    /// <summary>
    /// Gets the icon glyph character or the relative path to an icon file.
    /// </summary>
    [JsonPropertyName("icon")]
    public string? Icon { get; init; }

    /// <summary>
    /// Gets the optional entry point override that takes precedence over the top-level "main" field.
    /// </summary>
    [JsonPropertyName("main")]
    public string? Main { get; init; }

    /// <summary>
    /// Gets the author or publisher name.
    /// </summary>
    [JsonPropertyName("publisher")]
    public string? Publisher { get; init; }

    /// <summary>
    /// Gets a value indicating whether the Node.js process should start with the inspector attached.
    /// </summary>
    [JsonPropertyName("debug")]
    public bool Debug { get; init; }

    /// <summary>
    /// Gets the optional inspector port used when <see cref="Debug"/> is enabled.
    /// </summary>
    [JsonPropertyName("debugPort")]
    public int? DebugPort { get; init; }

    /// <summary>
    /// Gets the capabilities declared by the extension (for example, "commands").
    /// </summary>
    [JsonPropertyName("capabilities")]
    public string[]? Capabilities { get; init; }
}
