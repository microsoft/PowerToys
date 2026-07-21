// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

/// <summary>
/// Represents the raw top-level structure of a package.json used to discover
/// CmdPal JavaScript/TypeScript extensions.
/// </summary>
public sealed record JSPackageJson
{
    /// <summary>
    /// Gets the package identifier (npm "name"). Used as the extension id.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// Gets the semantic version string.
    /// </summary>
    [JsonPropertyName("version")]
    public string? Version { get; init; }

    /// <summary>
    /// Gets the package description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Gets the raw npm "author" field. It can be either a string
    /// (for example, "Jane Doe &lt;jane@example.com&gt; (https://example.com)")
    /// or an object with "name", "email", and "url" properties. Only the name is
    /// used, and only when cmdpal.publisher is absent.
    /// </summary>
    [JsonPropertyName("author")]
    public JsonElement? Author { get; init; }

    /// <summary>
    /// Gets the relative path to the entry point JavaScript file.
    /// </summary>
    [JsonPropertyName("main")]
    public string? Main { get; init; }

    /// <summary>
    /// Gets the engine requirements.
    /// </summary>
    [JsonPropertyName("engines")]
    public JSExtensionEngines? Engines { get; init; }

    /// <summary>
    /// Gets the CmdPal-specific metadata section.
    /// </summary>
    [JsonPropertyName("cmdpal")]
    public JSCmdPalSection? CmdPal { get; init; }
}
