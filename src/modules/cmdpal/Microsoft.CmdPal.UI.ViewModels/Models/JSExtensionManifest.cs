// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

/// <summary>
/// Represents a resolved CmdPal JavaScript/TypeScript extension manifest, built
/// from a package.json that contains a "cmdpal" section.
/// </summary>
public sealed record JSExtensionManifest
{
    /// <summary>
    /// Gets the extension identifier (package.json "name").
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets the display name (cmdpal.displayName), or null when not provided.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Gets the version string (package.json "version").
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Gets the description (package.json "description").
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the icon glyph or relative path (cmdpal.icon).
    /// </summary>
    public string? Icon { get; init; }

    /// <summary>
    /// Gets the author or publisher name (cmdpal.publisher).
    /// </summary>
    public string? Publisher { get; init; }

    /// <summary>
    /// Gets the entry point path as declared in the manifest (cmdpal.main overrides top-level main).
    /// </summary>
    public string? Main { get; init; }

    /// <summary>
    /// Gets the resolved absolute path to the entry point file.
    /// </summary>
    public string? EntryPointPath { get; init; }

    /// <summary>
    /// Gets a value indicating whether the Node.js process should start with the inspector attached.
    /// </summary>
    public bool Debug { get; init; }

    /// <summary>
    /// Gets the optional inspector port used when <see cref="Debug"/> is enabled.
    /// </summary>
    public int? DebugPort { get; init; }

    /// <summary>
    /// Gets the engine requirements (package.json "engines").
    /// </summary>
    public JSExtensionEngines? Engines { get; init; }

    /// <summary>
    /// Gets the effective display name, falling back to <see cref="Name"/> when no display name is set.
    /// </summary>
    public string EffectiveDisplayName => string.IsNullOrWhiteSpace(DisplayName) ? Name ?? string.Empty : DisplayName;

    /// <summary>
    /// Reads and validates a package.json file as a CmdPal extension manifest.
    /// </summary>
    /// <param name="packageJsonPath">The full path to the package.json file.</param>
    /// <returns>A result describing success (with the manifest) or the reason for failure.</returns>
    public static JSExtensionManifestParseResult TryParseFile(string packageJsonPath)
    {
        if (string.IsNullOrWhiteSpace(packageJsonPath))
        {
            return JSExtensionManifestParseResult.Failure("The package.json path was null or empty.");
        }

        if (!File.Exists(packageJsonPath))
        {
            return JSExtensionManifestParseResult.Failure($"No package.json was found at '{packageJsonPath}'.");
        }

        string json;
        try
        {
            json = File.ReadAllText(packageJsonPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return JSExtensionManifestParseResult.Failure($"Failed to read '{packageJsonPath}': {ex.Message}");
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(packageJsonPath)) ?? string.Empty;
        return TryParse(json, directory);
    }

    /// <summary>
    /// Parses and validates a package.json body as a CmdPal extension manifest.
    /// </summary>
    /// <param name="packageJson">The raw package.json contents.</param>
    /// <param name="extensionDirectory">The directory used to resolve the entry point file, checked for existence.</param>
    /// <returns>A result describing success (with the manifest) or the reason for failure.</returns>
    public static JSExtensionManifestParseResult TryParse(string packageJson, string extensionDirectory)
    {
        if (string.IsNullOrWhiteSpace(packageJson))
        {
            return JSExtensionManifestParseResult.Failure("The package.json contents were null or empty.");
        }

        JSPackageJson? package;
        try
        {
            package = JsonSerializer.Deserialize(packageJson, JSExtensionManifestJsonContext.Default.JSPackageJson);
        }
        catch (JsonException ex)
        {
            return JSExtensionManifestParseResult.Failure($"The package.json was not valid JSON: {ex.Message}");
        }

        if (package is null)
        {
            return JSExtensionManifestParseResult.Failure("The package.json deserialized to null.");
        }

        // Rule 1: a "cmdpal" object must be present (even if empty).
        if (package.CmdPal is null)
        {
            return JSExtensionManifestParseResult.Failure("The package.json does not contain a 'cmdpal' section.");
        }

        // Rule 2: "name" must be present and non-empty.
        if (string.IsNullOrWhiteSpace(package.Name))
        {
            return JSExtensionManifestParseResult.Failure("The package.json 'name' field is missing or empty.");
        }

        // Rule 3: either cmdpal.main or top-level main must resolve to an existing file.
        var entryPoint = !string.IsNullOrWhiteSpace(package.CmdPal.Main)
            ? package.CmdPal.Main
            : package.Main;

        if (string.IsNullOrWhiteSpace(entryPoint))
        {
            return JSExtensionManifestParseResult.Failure("Neither 'cmdpal.main' nor the top-level 'main' entry point was specified.");
        }

        var resolvedEntryPoint = ResolveEntryPoint(extensionDirectory, entryPoint!, out var resolutionError);
        if (resolvedEntryPoint is null)
        {
            return JSExtensionManifestParseResult.Failure(resolutionError!);
        }

        if (!File.Exists(resolvedEntryPoint))
        {
            return JSExtensionManifestParseResult.Failure($"The entry point '{entryPoint}' does not resolve to an existing file.");
        }

        var manifest = new JSExtensionManifest
        {
            Name = package.Name,
            DisplayName = package.CmdPal.DisplayName,
            Version = package.Version,
            Description = package.Description,
            Icon = package.CmdPal.Icon,
            Publisher = ResolvePublisher(package),
            Main = entryPoint,
            EntryPointPath = resolvedEntryPoint,
            Debug = package.CmdPal.Debug,
            DebugPort = package.CmdPal.DebugPort,
            Engines = package.Engines,
        };

        return JSExtensionManifestParseResult.Success(manifest);
    }

    /// <summary>
    /// Resolves the publisher name. The explicit cmdpal.publisher value wins. When it is
    /// absent or whitespace, the name portion of the top-level npm "author" field is used.
    /// Returns null when neither source provides a name.
    /// </summary>
    private static string? ResolvePublisher(JSPackageJson package)
    {
        if (!string.IsNullOrWhiteSpace(package.CmdPal?.Publisher))
        {
            return package.CmdPal.Publisher;
        }

        return ExtractAuthorName(package.Author);
    }

    /// <summary>
    /// Extracts the author name from the npm "author" field. The field is either a string
    /// such as "Jane Doe &lt;jane@example.com&gt; (https://example.com)" or an object with a
    /// "name" property. For the string form, the substring before the first '&lt;' or '('
    /// delimiter is taken. Returns null when no usable name can be determined.
    /// </summary>
    private static string? ExtractAuthorName(JsonElement? author)
    {
        if (author is not { } authorElement)
        {
            return null;
        }

        switch (authorElement.ValueKind)
        {
            case JsonValueKind.String:
                var raw = authorElement.GetString();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return null;
                }

                var end = raw.AsSpan().IndexOfAny('<', '(');
                var name = (end >= 0 ? raw[..end] : raw).Trim();
                return string.IsNullOrEmpty(name) ? null : name;

            case JsonValueKind.Object:
                if (authorElement.TryGetProperty("name", out var nameElement) &&
                    nameElement.ValueKind == JsonValueKind.String)
                {
                    var objectName = nameElement.GetString();
                    return string.IsNullOrWhiteSpace(objectName) ? null : objectName.Trim();
                }

                return null;

            default:
                return null;
        }
    }

    private static string? ResolveEntryPoint(string extensionDirectory, string entryPoint, out string? error)
    {
        error = null;

        // The spec requires the entry point to be a relative path within the extension
        // directory. Reject absolute/rooted paths so a manifest cannot point outside its package.
        if (Path.IsPathRooted(entryPoint))
        {
            error = $"The entry point '{entryPoint}' must be a relative path within the extension directory.";
            return null;
        }

        if (string.IsNullOrEmpty(extensionDirectory))
        {
            error = "An extension directory is required to resolve the entry point.";
            return null;
        }

        string baseDirectory;
        string resolved;
        try
        {
            baseDirectory = Path.GetFullPath(extensionDirectory);
            resolved = Path.GetFullPath(Path.Combine(baseDirectory, entryPoint));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            error = $"The entry point '{entryPoint}' is not a valid path.";
            return null;
        }

        // Guard against traversal via "..": the resolved path must stay inside the extension directory.
        var prefix = baseDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? baseDirectory
            : baseDirectory + Path.DirectorySeparatorChar;

        if (!resolved.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            error = $"The entry point '{entryPoint}' must not escape the extension directory.";
            return null;
        }

        return resolved;
    }
}
