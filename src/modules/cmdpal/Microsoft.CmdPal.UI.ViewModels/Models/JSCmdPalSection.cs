// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

/// <summary>
/// The "cmdpal" section of a package.json for a CmdPal JavaScript/TypeScript extension.
/// </summary>
public sealed record JSCmdPalSection
{
    /// <summary>
    /// Gets the name shown in CmdPal. Falls back to the package name when absent.
    /// </summary>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    /// <summary>
    /// Gets the icon glyph character or the relative path to an icon file.
    /// </summary>
    [JsonPropertyName("icon")]
    public string? Icon { get; init; }

    /// <summary>
    /// Gets the optional entry point override. This takes precedence over the top-level "main" field.
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
    /// Gets the optional relative directory the host should watch for hot-reload source
    /// changes. When absent, the host watches the directory containing the resolved entry
    /// point instead of the whole package, so directories the extension never declared
    /// (version-control metadata, docs, generated artifacts unrelated to the entry point)
    /// are not swept in by a host guess.
    /// </summary>
    [JsonPropertyName("watchPath")]
    public string? WatchPath { get; init; }
}
