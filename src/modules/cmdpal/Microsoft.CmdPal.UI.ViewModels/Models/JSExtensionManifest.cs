// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

/// <summary>
/// Represents the manifest file (cmdpal.json) for a JavaScript/TypeScript extension.
/// </summary>
public sealed record JSExtensionManifest
{
    /// <summary>
    /// Gets the internal identifier for the extension (required).
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// Gets the display name shown to users.
    /// </summary>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    /// <summary>
    /// Gets the version string (e.g., "1.0.0").
    /// </summary>
    [JsonPropertyName("version")]
    public string? Version { get; init; }

    /// <summary>
    /// Gets the description of the extension.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Gets the relative path to the icon file.
    /// </summary>
    [JsonPropertyName("icon")]
    public string? Icon { get; init; }

    /// <summary>
    /// Gets the entry point script path (required).
    /// </summary>
    [JsonPropertyName("main")]
    public string? Main { get; init; }

    /// <summary>
    /// Gets the publisher or author name.
    /// </summary>
    [JsonPropertyName("publisher")]
    public string? Publisher { get; init; }

    /// <summary>
    /// Gets a value indicating whether debug mode is enabled for this extension.
    /// When true, the Node.js process is started with --inspect to allow debugger attachment.
    /// </summary>
    [JsonPropertyName("debug")]
    public bool Debug { get; init; }

    /// <summary>
    /// Gets the port number for the Node.js inspector when debug mode is enabled.
    /// If not specified, a default port starting at 9229 is assigned automatically.
    /// </summary>
    [JsonPropertyName("debugPort")]
    public int? DebugPort { get; init; }

    /// <summary>
    /// Gets the engine requirements (e.g., Node.js version).
    /// </summary>
    [JsonPropertyName("engines")]
    public JSExtensionEngines? Engines { get; init; }

    /// <summary>
    /// Gets the capabilities exposed by the extension.
    /// Capabilities are a simple list of strings (e.g., ["commands", "listPages", "contentPages"]).
    /// </summary>
    [JsonPropertyName("capabilities")]
    public string[]? Capabilities { get; init; }

    /// <summary>
    /// Validates that required fields are present.
    /// </summary>
    /// <returns>True if the manifest is valid; otherwise, false.</returns>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(Main);
    }

    /// <summary>
    /// Loads and deserializes a manifest file from the specified path.
    /// </summary>
    /// <param name="manifestPath">The full path to the cmdpal.json file.</param>
    /// <returns>The deserialized manifest if successful and valid; otherwise, null.</returns>
    public static async Task<JSExtensionManifest?> LoadFromFileAsync(string manifestPath)
    {
        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            return null;
        }

        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(manifestPath);
            var manifest = await JsonSerializer.DeserializeAsync(
                stream,
                JSExtensionManifestJsonContext.Default.JSExtensionManifest);

            if (manifest is null || !manifest.IsValid())
            {
                return null;
            }

            return manifest;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Represents the engine requirements for a JavaScript/TypeScript extension.
/// </summary>
public sealed record JSExtensionEngines
{
    /// <summary>
    /// Gets the Node.js version requirement (e.g., ">=18").
    /// </summary>
    [JsonPropertyName("node")]
    public string? Node { get; init; }
}

/// <summary>
/// JSON serialization context for JSExtensionManifest and related types.
/// </summary>
[JsonSerializable(typeof(JSExtensionManifest))]
[JsonSerializable(typeof(JSExtensionEngines))]
[JsonSerializable(typeof(string[]))]
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, AllowTrailingCommas = true)]
internal sealed partial class JSExtensionManifestJsonContext : JsonSerializerContext
{
}
