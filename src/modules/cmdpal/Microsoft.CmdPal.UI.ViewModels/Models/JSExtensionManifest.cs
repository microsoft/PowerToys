// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

/// <summary>
/// A resolved CmdPal JavaScript/TypeScript extension manifest built from a package.json
/// with a "cmdpal" section.
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
    /// Gets the resolved absolute directory the host should watch for hot-reload source
    /// changes (from cmdpal.watchPath), or null when the manifest does not declare one. A
    /// null value means the caller falls back to the directory containing
    /// <see cref="EntryPointPath"/> rather than the whole extension directory, so hot-reload
    /// scope is driven by what the extension declared instead of a host guess.
    /// </summary>
    public string? WatchDirectory { get; init; }

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
    /// Gets the stable key used to compare extensions for uniqueness. The package
    /// <see cref="Name"/> is trimmed and lower-cased to match npm package-name comparisons.
    /// Discovery rejects two installed extensions that resolve to the same key. The parser only
    /// reports the key.
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
    /// Unknown JSON fields are ignored so a newer manifest can still parse on an older host.
    /// Missing required fields and malformed value types return a failed
    /// <see cref="JSExtensionManifestParseResult"/> rather than throwing. The extension
    /// <see cref="Name"/> is the identity. The phase 4 discovery service rejects duplicate
    /// identities across installed extensions.
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

        // Rule 4: Node must be able to run the entry point directly. Only .js, .mjs, and .cjs are
        // supported. Uncompiled .ts source is rejected.
        if (!IsSupportedEntryPointExtension(resolvedEntryPoint))
        {
            return JSExtensionManifestParseResult.Failure($"The entry point '{entryPoint}' must be a JavaScript file with a .js, .mjs, or .cjs extension.");
        }

        if (!File.Exists(resolvedEntryPoint))
        {
            return JSExtensionManifestParseResult.Failure($"The entry point '{entryPoint}' does not resolve to an existing file.");
        }

        // Rule 5: a symbolic link or junction must not redirect the entry point outside the extension
        // directory, even when the text path stays inside it. Check the real filesystem after the file
        // is known to exist.
        if (!IsEntryPointContainmentTrusted(extensionDirectory, resolvedEntryPoint, out var containmentError))
        {
            return JSExtensionManifestParseResult.Failure(containmentError!);
        }

        // Rule 6: an optional cmdpal.watchPath narrows (or relocates) the host's hot-reload
        // scope. It is validated the same way as the entry point: it must be a relative path
        // that resolves to an existing directory inside the extension directory, and it must
        // not reach outside that directory through a symbolic link or junction. Absent, the
        // caller falls back to the entry point's own directory.
        string? watchDirectory = null;
        if (!string.IsNullOrWhiteSpace(package.CmdPal.WatchPath))
        {
            watchDirectory = ResolveWatchDirectory(extensionDirectory, package.CmdPal.WatchPath, out var watchPathError);
            if (watchDirectory is null)
            {
                return JSExtensionManifestParseResult.Failure(watchPathError!);
            }
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
            WatchDirectory = watchDirectory,
            Debug = package.CmdPal.Debug,
            DebugPort = package.CmdPal.DebugPort,
            Engines = package.Engines,
        };

        return JSExtensionManifestParseResult.Success(manifest);
    }

    /// <summary>
    /// Resolves the publisher name. The explicit cmdpal.publisher value wins. When it is absent or
    /// whitespace, the name portion of the top-level npm "author" field is used. Returns null when
    /// neither source provides a name.
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
    /// Extracts the author name from the npm "author" field. The field is either a string such as
    /// "Jane Doe &lt;jane@example.com&gt; (https://example.com)" or an object with a "name" property.
    /// For the string form, text before the first '&lt;' or '(' delimiter is used. Returns null when
    /// no usable name can be found.
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

        // The entry point has to stay relative to the extension directory. Reject rooted paths so a
        // manifest cannot point outside its package.
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

        // Guard against ".." traversal. The resolved path must stay inside the extension directory.
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

    /// <summary>
    /// Resolves and validates cmdpal.watchPath the same way <see cref="ResolveEntryPoint"/>
    /// resolves the entry point: it must be a relative path within the extension directory
    /// that does not traverse (via "..") or, once resolved, redirect through a reparse point
    /// outside the extension directory. Unlike the entry point it must resolve to a directory,
    /// not a file, since it names the host's hot-reload watch root.
    /// </summary>
    private static string? ResolveWatchDirectory(string extensionDirectory, string watchPath, out string? error)
    {
        error = null;

        if (Path.IsPathRooted(watchPath))
        {
            error = $"The 'cmdpal.watchPath' value '{watchPath}' must be a relative path within the extension directory.";
            return null;
        }

        string baseDirectory;
        string resolved;
        try
        {
            baseDirectory = Path.GetFullPath(extensionDirectory);
            resolved = Path.GetFullPath(Path.Combine(baseDirectory, watchPath));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            error = $"The 'cmdpal.watchPath' value '{watchPath}' is not a valid path.";
            return null;
        }

        var prefix = baseDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? baseDirectory
            : baseDirectory + Path.DirectorySeparatorChar;

        if (!resolved.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Path.TrimEndingDirectorySeparator(resolved), Path.TrimEndingDirectorySeparator(baseDirectory), StringComparison.OrdinalIgnoreCase))
        {
            error = $"The 'cmdpal.watchPath' value '{watchPath}' must not escape the extension directory.";
            return null;
        }

        if (!Directory.Exists(resolved))
        {
            error = $"The 'cmdpal.watchPath' value '{watchPath}' does not resolve to an existing directory.";
            return null;
        }

        // Reuse the same reparse-point walk used for the entry point: it only compares the
        // resolved path against the extension directory as it walks up, so it works equally
        // well for a directory as for a file.
        if (!IsEntryPointContainmentTrusted(extensionDirectory, resolved, out _))
        {
            error = $"The 'cmdpal.watchPath' value '{watchPath}' traverses a symbolic link or junction, which is not allowed.";
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
    /// filesystem. The text check in <see cref="ResolveEntryPoint"/> only blocks ".." traversal. A
    /// symbolic link or junction could still redirect an in-package path outside the package, so any
    /// reparse point between the extension directory and the entry point is rejected.
    /// </summary>
    private static bool IsEntryPointContainmentTrusted(string extensionDirectory, string resolvedEntryPoint, out string? error)
    {
        error = null;

        try
        {
            var baseDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(extensionDirectory));
            var current = Path.GetFullPath(resolvedEntryPoint);

            // Walk from the entry point up toward the extension directory. The extension directory
            // itself and everything above it are outside this check.
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
                    // Reached a filesystem root without meeting the extension directory. The text
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
            // A missing segment cannot be a trusted but unverified link. The caller already confirmed
            // the entry point exists, so treat a vanished segment as not a reparse point.
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException)
        {
            // If the segment's attributes cannot be read, err on the side of caution and reject it.
            return true;
        }
    }
}
