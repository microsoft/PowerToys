// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma warning disable SA1402 // File may only contain a single type - manifest and related types grouped together

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

/// <summary>
/// Represents the resolved extension manifest, built from a package.json file
/// that contains a "cmdpal" section (similar to VS Code's contributes field).
/// </summary>
public sealed record JSExtensionManifest
{
    /// <summary>
    /// Gets the internal identifier for the extension (from package.json "name", required).
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets the display name shown to users (from cmdpal.displayName).
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Gets the version string (from package.json "version").
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Gets the description of the extension (from package.json "description").
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the icon glyph or relative path (from cmdpal.icon).
    /// </summary>
    public string? Icon { get; init; }

    /// <summary>
    /// Gets the entry point script path (cmdpal.main overrides package.json "main", required).
    /// </summary>
    public string? Main { get; init; }

    /// <summary>
    /// Gets the publisher or author name (from cmdpal.publisher).
    /// </summary>
    public string? Publisher { get; init; }

    /// <summary>
    /// Gets a value indicating whether debug mode is enabled for this extension.
    /// When true, the Node.js process is started with --inspect to allow debugger attachment.
    /// </summary>
    public bool Debug { get; init; }

    /// <summary>
    /// Gets the port number for the Node.js inspector when debug mode is enabled.
    /// If not specified, a default port starting at 9229 is assigned automatically.
    /// </summary>
    public int? DebugPort { get; init; }

    /// <summary>
    /// Gets the engine requirements (from package.json "engines").
    /// </summary>
    public JSExtensionEngines? Engines { get; init; }

    /// <summary>
    /// Gets the capabilities exposed by the extension (from cmdpal.capabilities).
    /// </summary>
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
    /// Loads an extension manifest from a package.json file that contains a "cmdpal" section.
    /// </summary>
    /// <param name="packageJsonPath">The full path to the package.json file.</param>
    /// <returns>The resolved manifest if successful and valid; otherwise, null.</returns>
    public static async Task<JSExtensionManifest?> LoadFromFileAsync(string packageJsonPath)
    {
        if (string.IsNullOrWhiteSpace(packageJsonPath))
        {
            return null;
        }

        if (!File.Exists(packageJsonPath))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(packageJsonPath);
            var packageJson = await JsonSerializer.DeserializeAsync(
                stream,
                JSExtensionManifestJsonContext.Default.JSPackageJson);

            if (packageJson?.CmdPal is null)
            {
                return null;
            }

            var cmdpal = packageJson.CmdPal;

            // cmdpal.main overrides top-level main
            var entryPoint = !string.IsNullOrWhiteSpace(cmdpal.Main)
                ? cmdpal.Main
                : packageJson.Main;

            var manifest = new JSExtensionManifest
            {
                Name = packageJson.Name,
                DisplayName = cmdpal.DisplayName,
                Version = packageJson.Version,
                Description = packageJson.Description,
                Icon = cmdpal.Icon,
                Main = entryPoint,
                Publisher = cmdpal.Publisher,
                Debug = cmdpal.Debug,
                DebugPort = cmdpal.DebugPort,
                Engines = packageJson.Engines,
                Capabilities = cmdpal.Capabilities,
            };

            if (!manifest.IsValid())
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
/// Represents the top-level package.json structure for extension discovery.
/// </summary>
public sealed record JSPackageJson
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("version")]
    public string? Version { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("main")]
    public string? Main { get; init; }

    [JsonPropertyName("engines")]
    public JSExtensionEngines? Engines { get; init; }

    [JsonPropertyName("cmdpal")]
    public JSCmdPalSection? CmdPal { get; init; }
}

/// <summary>
/// Represents the "cmdpal" section within package.json containing CmdPal-specific metadata.
/// </summary>
public sealed record JSCmdPalSection
{
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("icon")]
    public string? Icon { get; init; }

    [JsonPropertyName("main")]
    public string? Main { get; init; }

    [JsonPropertyName("publisher")]
    public string? Publisher { get; init; }

    [JsonPropertyName("debug")]
    public bool Debug { get; init; }

    [JsonPropertyName("debugPort")]
    public int? DebugPort { get; init; }

    [JsonPropertyName("capabilities")]
    public string[]? Capabilities { get; init; }
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
/// JSON serialization context for extension manifest types.
/// </summary>
[JsonSerializable(typeof(JSPackageJson))]
[JsonSerializable(typeof(JSCmdPalSection))]
[JsonSerializable(typeof(JSExtensionManifest))]
[JsonSerializable(typeof(JSExtensionEngines))]
[JsonSerializable(typeof(string[]))]
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, AllowTrailingCommas = true)]
internal sealed partial class JSExtensionManifestJsonContext : JsonSerializerContext
{
}
