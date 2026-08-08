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
    /// Gets the icon glyph or relative path exactly as declared in the manifest (cmdpal.icon).
    /// </summary>
    public string? Icon { get; init; }

    /// <summary>
    /// Gets the full path to the package directory the manifest was parsed from. Relative
    /// resources declared in the manifest (such as <see cref="Icon"/>) are resolved against this
    /// directory and are required to stay within it.
    /// </summary>
    public string? RootDirectory { get; init; }

    /// <summary>
    /// Gets the effective icon reference the host should display. A glyph or a URI is carried
    /// through unchanged; a relative file path is resolved to a full path inside
    /// <see cref="RootDirectory"/>. A relative path that escapes the package, traverses a
    /// symbolic link or junction, or does not exist resolves to an empty string (no icon) so a
    /// manifest can never point the host at a file outside its own directory.
    /// </summary>
    public string? IconPath { get; init; }

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
    /// Gets the stable identity key used to compare extensions for uniqueness. The extension
    /// <see cref="Name"/> is the identity; it is trimmed and lower-cased so that comparisons are
    /// case-insensitive, matching npm package-name semantics. Cross-extension uniqueness (rejecting a
    /// second installed extension that resolves to the same key) is enforced during discovery, not by
    /// the manifest parser. See the discovery-level follow-up noted in <see cref="TryParse"/>.
    /// </summary>
    public string NameKey => (Name ?? string.Empty).Trim().ToLowerInvariant();

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
    /// <remarks>
    /// Unknown JSON fields are ignored rather than treated as errors, so a newer manifest still parses
    /// on an older host. Missing required fields and malformed value types produce a failed result
    /// through <see cref="JSExtensionManifestParseResult"/> rather than throwing. The extension
    /// <see cref="Name"/> is the extension identity (see <see cref="NameKey"/>); enforcing that two
    /// different installed extensions do not share the same identity is a discovery-level concern and
    /// is handled by the extension discovery service (phase 4), not by this parser.
    /// </remarks>
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

        // Rule 4: the entry point must be a JavaScript module Node can execute directly. Only .js,
        // .mjs, and .cjs are supported; anything else (for example an uncompiled .ts source) is rejected.
        if (!IsSupportedEntryPointExtension(resolvedEntryPoint))
        {
            return JSExtensionManifestParseResult.Failure($"The entry point '{entryPoint}' must be a JavaScript file with a .js, .mjs, or .cjs extension.");
        }

        if (!File.Exists(resolvedEntryPoint))
        {
            return JSExtensionManifestParseResult.Failure($"The entry point '{entryPoint}' does not resolve to an existing file.");
        }

        // Rule 5: a symbolic link or junction must not redirect the entry point outside the extension
        // directory, even when the lexical path stays within it. This is checked against the real
        // filesystem after confirming the file exists.
        if (!IsEntryPointContainmentTrusted(extensionDirectory, resolvedEntryPoint, out var containmentError))
        {
            return JSExtensionManifestParseResult.Failure(containmentError!);
        }

        var manifest = new JSExtensionManifest
        {
            Name = package.Name,
            DisplayName = package.CmdPal.DisplayName,
            Version = package.Version,
            Description = package.Description,
            Icon = package.CmdPal.Icon,
            RootDirectory = ResolveRootDirectory(extensionDirectory),
            IconPath = ResolveManifestIcon(extensionDirectory, package.CmdPal.Icon),
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

    /// <summary>
    /// Resolves the package root directory used to anchor relative manifest resources. Returns
    /// null when no directory is available.
    /// </summary>
    private static string? ResolveRootDirectory(string extensionDirectory)
    {
        if (string.IsNullOrEmpty(extensionDirectory))
        {
            return null;
        }

        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(extensionDirectory));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    /// <summary>
    /// Resolves the effective icon reference. A glyph, an empty value, or a URI is returned
    /// unchanged. A relative file path is resolved against <paramref name="extensionDirectory"/>
    /// and must stay inside it: a path that escapes the package (via ".." or a symbolic
    /// link/junction) or that does not exist resolves to an empty string so the host shows no
    /// icon rather than loading a file from outside the package.
    /// </summary>
    private static string? ResolveManifestIcon(string extensionDirectory, string? icon)
    {
        if (string.IsNullOrWhiteSpace(icon))
        {
            return icon;
        }

        var trimmed = icon.Trim();

        // Only a relative file path is resolved; a glyph, emoji, absolute path, or URI is left
        // exactly as authored so existing behavior for those forms is unchanged.
        if (!LooksLikeRelativeFilePath(trimmed))
        {
            return trimmed;
        }

        if (string.IsNullOrEmpty(extensionDirectory))
        {
            return string.Empty;
        }

        string baseDirectory;
        string resolved;
        try
        {
            baseDirectory = Path.GetFullPath(extensionDirectory);
            resolved = Path.GetFullPath(Path.Combine(baseDirectory, trimmed));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return string.Empty;
        }

        // Lexical containment: the resolved icon must stay within the package directory.
        var prefix = baseDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? baseDirectory
            : baseDirectory + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        if (!File.Exists(resolved))
        {
            return string.Empty;
        }

        // Real-filesystem containment: a symbolic link or junction must not redirect the icon out
        // of the package, even when the lexical path stays within it.
        if (!IsEntryPointContainmentTrusted(extensionDirectory, resolved, out _))
        {
            return string.Empty;
        }

        return resolved;
    }

    /// <summary>
    /// Determines whether an icon value should be treated as a relative file path (rather than a
    /// glyph, emoji, absolute path, or URI). A relative path either has a known image extension or
    /// contains a directory separator, and is neither rooted nor a URI.
    /// </summary>
    private static bool LooksLikeRelativeFilePath(string icon)
    {
        if (icon.Contains("://", StringComparison.Ordinal))
        {
            return false;
        }

        if (Path.IsPathRooted(icon))
        {
            return false;
        }

        if (icon.Contains('/') || icon.Contains('\\'))
        {
            return true;
        }

        var extension = Path.GetExtension(icon.AsSpan());
        foreach (var imageExtension in ImageFileExtensions)
        {
            if (extension.Equals(imageExtension, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static readonly string[] ImageFileExtensions =
    [
        ".png",
        ".jpg",
        ".jpeg",
        ".gif",
        ".bmp",
        ".ico",
        ".svg",
        ".webp",
    ];

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

    private static bool IsSupportedEntryPointExtension(string path)
    {
        var extension = Path.GetExtension(path.AsSpan());
        return extension.Equals(".js", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".mjs", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".cjs", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Confirms that the resolved entry point stays inside the extension directory on the real
    /// filesystem. The lexical check in <see cref="ResolveEntryPoint"/> only blocks ".." traversal; a
    /// symbolic link or junction could still redirect a lexically-contained path outside the package.
    /// Any reparse point encountered between the extension directory and the entry point is rejected.
    /// </summary>
    private static bool IsEntryPointContainmentTrusted(string extensionDirectory, string resolvedEntryPoint, out string? error)
    {
        error = null;

        try
        {
            var baseDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(extensionDirectory));
            var current = Path.GetFullPath(resolvedEntryPoint);

            // Walk from the entry point up toward the extension directory. The extension directory
            // itself and everything above it are outside the scope of this check.
            while (!string.Equals(Path.TrimEndingDirectorySeparator(current), baseDirectory, StringComparison.OrdinalIgnoreCase))
            {
                if (IsReparsePoint(current))
                {
                    error = $"The entry point '{resolvedEntryPoint}' traverses a symbolic link or junction, which is not allowed.";
                    return false;
                }

                var parent = Path.GetDirectoryName(current);
                if (string.IsNullOrEmpty(parent) || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
                {
                    // Reached a filesystem root without meeting the extension directory. The lexical
                    // containment check already ran, so this only happens for pathological inputs.
                    break;
                }

                current = parent;
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or PathTooLongException or System.Security.SecurityException)
        {
            error = $"The entry point '{resolvedEntryPoint}' could not be validated: {ex.Message}";
            return false;
        }
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            // A missing segment cannot be a trusted-but-unverified link. Existence of the entry point
            // was already confirmed by the caller, so treat a vanished segment as not a reparse point.
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException)
        {
            // If the segment's attributes cannot be read, err on the side of caution and reject it.
            return true;
        }
    }
}
