// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels.Models;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// Reconciles the on-disk layout that "npm install &lt;package&gt;" produces with the
/// layout the <see cref="JsonRpcExtensionService"/> discovery scan expects.
/// <para>
/// When npm installs a package into <c>JSExtensions/&lt;name&gt;</c>, the extension's
/// package.json (the one that carries the <c>cmdpal</c> section) lands under
/// <c>JSExtensions/&lt;name&gt;/node_modules/&lt;package&gt;/package.json</c>, while discovery
/// only scans <c>JSExtensions/&lt;name&gt;/package.json</c>. This helper "materializes"
/// (hoists) the installed package to the target root so the directory matches the
/// documented layout: a package.json with the <c>cmdpal</c> section, a <c>dist/</c>
/// folder, and a <c>node_modules/</c> folder holding the runtime dependencies.
/// </para>
/// </summary>
internal static class JsExtensionPackageLayout
{
    private const string NodeModulesName = "node_modules";
    private const string PackageJsonName = "package.json";

    /// <summary>
    /// Hoists the CmdPal package that npm installed under
    /// <paramref name="targetDirectory"/>/node_modules up to
    /// <paramref name="targetDirectory"/> itself so it is discoverable.
    /// </summary>
    /// <param name="targetDirectory">The extension directory npm installed into.</param>
    /// <returns>A result describing success or the reason the package could not be materialized.</returns>
    public static NpmCommandResult Materialize(string targetDirectory)
    {
        if (string.IsNullOrWhiteSpace(targetDirectory) || !Directory.Exists(targetDirectory))
        {
            return NpmCommandResult.Fail("The extension directory does not exist after installation.");
        }

        // If the root already carries a valid manifest (a re-run, or an unusual package
        // that npm placed at the root), there is nothing to hoist.
        if (HasValidManifest(targetDirectory))
        {
            return NpmCommandResult.Ok();
        }

        var nodeModules = Path.Combine(targetDirectory, NodeModulesName);
        if (!Directory.Exists(nodeModules))
        {
            return NpmCommandResult.Fail("npm did not produce a node_modules folder.");
        }

        var packageDirectory = FindInstalledCmdPalPackage(nodeModules);
        if (packageDirectory is null)
        {
            return NpmCommandResult.Fail("The installed npm package is not a Command Palette extension.");
        }

        try
        {
            HoistPackage(packageDirectory, targetDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.LogError($"Failed to materialize JS extension in {targetDirectory}: {ex.Message}");
            return NpmCommandResult.Fail($"Could not prepare the extension files: {ex.Message}");
        }

        if (!HasValidManifest(targetDirectory))
        {
            return NpmCommandResult.Fail("The extension could not be prepared into a discoverable layout.");
        }

        return NpmCommandResult.Ok();
    }

    private static bool HasValidManifest(string directory)
    {
        var manifestPath = Path.Combine(directory, PackageJsonName);
        if (!File.Exists(manifestPath))
        {
            return false;
        }

        return JSExtensionManifest.TryParseFile(manifestPath).IsValid;
    }

    /// <summary>
    /// Scans a node_modules directory (including scoped <c>@scope/name</c> folders) for the
    /// single package whose package.json carries a valid <c>cmdpal</c> manifest.
    /// </summary>
    /// <param name="nodeModules">The node_modules directory to scan.</param>
    /// <returns>The package directory, or null when no CmdPal package is present.</returns>
    internal static string? FindInstalledCmdPalPackage(string nodeModules)
    {
        foreach (var candidate in EnumeratePackageDirectories(nodeModules))
        {
            if (HasValidManifest(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumeratePackageDirectories(string nodeModules)
    {
        string[] entries;
        try
        {
            entries = Directory.GetDirectories(nodeModules);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.LogError($"Failed to enumerate {nodeModules}: {ex.Message}");
            yield break;
        }

        foreach (var entry in entries)
        {
            var name = Path.GetFileName(entry);

            // npm stores scoped packages under node_modules/@scope/name; recurse one level.
            if (name.StartsWith('@'))
            {
                string[] scoped;
                try
                {
                    scoped = Directory.GetDirectories(entry);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Logger.LogError($"Failed to enumerate {entry}: {ex.Message}");
                    continue;
                }

                foreach (var scopedEntry in scoped)
                {
                    yield return scopedEntry;
                }

                continue;
            }

            if (string.Equals(name, ".bin", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return entry;
        }
    }

    /// <summary>
    /// Moves every entry of <paramref name="packageDirectory"/> up to
    /// <paramref name="targetDirectory"/>. The package's own <c>node_modules</c> children are
    /// merged into the sibling <c>node_modules</c> that already holds the hoisted dependencies,
    /// so Node.js module resolution keeps working from the new package root.
    /// </summary>
    private static void HoistPackage(string packageDirectory, string targetDirectory)
    {
        foreach (var entry in Directory.GetFileSystemEntries(packageDirectory))
        {
            var name = Path.GetFileName(entry);
            var destination = Path.Combine(targetDirectory, name);

            if (Directory.Exists(entry))
            {
                if (string.Equals(name, NodeModulesName, StringComparison.OrdinalIgnoreCase))
                {
                    MergeDirectory(entry, destination);
                    continue;
                }

                if (Directory.Exists(destination))
                {
                    Directory.Delete(destination, recursive: true);
                }

                Directory.Move(entry, destination);
            }
            else
            {
                if (File.Exists(destination))
                {
                    File.Delete(destination);
                }

                File.Move(entry, destination);
            }
        }

        // The package folder is now empty; remove it, and the parent @scope folder if it is empty.
        var parent = Path.GetDirectoryName(packageDirectory);
        Directory.Delete(packageDirectory, recursive: true);
        if (parent is not null
            && Path.GetFileName(parent).StartsWith('@')
            && Directory.Exists(parent)
            && Directory.GetFileSystemEntries(parent).Length == 0)
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    private static void MergeDirectory(string source, string destination)
    {
        if (!Directory.Exists(destination))
        {
            Directory.Move(source, destination);
            return;
        }

        foreach (var child in Directory.GetFileSystemEntries(source))
        {
            var name = Path.GetFileName(child);
            var childDestination = Path.Combine(destination, name);

            if (Directory.Exists(child))
            {
                if (Directory.Exists(childDestination))
                {
                    // Keep the already-hoisted dependency; skip the nested duplicate.
                    continue;
                }

                Directory.Move(child, childDestination);
            }
            else if (!File.Exists(childDestination))
            {
                File.Move(child, childDestination);
            }
        }

        Directory.Delete(source, recursive: true);
    }
}
