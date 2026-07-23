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
/// Single, identity-aware source of truth for turning the on-disk tree that
/// "npm install &lt;package&gt;" produces into the layout the
/// <see cref="JsonRpcExtensionService"/> discovery scan expects.
/// <para>
/// npm installs the approved package under
/// <c>&lt;installRoot&gt;/node_modules/&lt;package&gt;</c> and hoists its runtime
/// dependencies to <c>&lt;installRoot&gt;/node_modules</c>. Discovery, however, expects the
/// package itself at the extension root, with a package.json that carries the
/// <c>cmdpal</c> section, a compiled entry point, and a <c>node_modules</c> folder holding
/// the runtime dependencies.
/// </para>
/// <para>
/// <see cref="ResolveRequestedPackage"/> resolves the exact requested identity (scoped
/// <c>@scope/name</c> and unscoped) and refuses an ambiguous layout that carries more than
/// one loadable Command Palette package. <see cref="AssembleDiscoveryLayout"/> then builds
/// the discoverable tree in a staging-local directory so the caller can promote it
/// atomically. The <see cref="NpmJsExtensionInstaller"/> is the only production caller, so
/// the identity and hoisting rules live here in one place rather than being duplicated by
/// the installer.
/// </para>
/// </summary>
internal static class JsExtensionPackageLayout
{
    private const string NodeModulesName = "node_modules";
    private const string PackageJsonName = "package.json";
    private const string AssembledDirectoryName = "__cmdpal_assembled";

    /// <summary>
    /// The outcome of resolving the requested package under an install root.
    /// </summary>
    internal readonly record struct PackageResolution(bool Succeeded, string? PackageDirectory, string? ErrorMessage)
    {
        public static PackageResolution Ok(string packageDirectory) => new(true, packageDirectory, null);

        public static PackageResolution Fail(string errorMessage) => new(false, null, errorMessage);
    }

    /// <summary>
    /// Resolves the directory that holds the requested package under
    /// <paramref name="installRoot"/>/node_modules. The lookup is identity-aware: it resolves
    /// the exact requested name (honoring scoped <c>@scope/name</c> packages) rather than
    /// picking the first Command Palette package it finds, and it rejects a layout that carries
    /// more than one loadable Command Palette package so an unrelated dependency can never be
    /// promoted in place of the approved extension.
    /// </summary>
    /// <param name="installRoot">The staging directory npm installed into.</param>
    /// <param name="requestedPackage">The approved npm package identity (for example, "@scope/name").</param>
    /// <returns>A resolution describing the package directory or the reason it could not be resolved.</returns>
    public static PackageResolution ResolveRequestedPackage(string installRoot, string requestedPackage)
    {
        if (string.IsNullOrWhiteSpace(installRoot) || !Directory.Exists(installRoot))
        {
            return PackageResolution.Fail("The install directory does not exist.");
        }

        if (string.IsNullOrWhiteSpace(requestedPackage))
        {
            return PackageResolution.Fail("A package identity is required to resolve the installed extension.");
        }

        var nodeModules = Path.Combine(installRoot, NodeModulesName);
        if (!Directory.Exists(nodeModules))
        {
            return PackageResolution.Fail("npm did not produce a node_modules folder.");
        }

        // Resolve the exact requested identity. A scoped name uses a forward slash on the wire
        // (npm) which maps to a nested @scope/name directory on disk.
        var normalizedNodeModules = Path.TrimEndingDirectorySeparator(Path.GetFullPath(nodeModules));
        string requestedDirectory;
        try
        {
            requestedDirectory = Path.GetFullPath(
                Path.Combine(nodeModules, requestedPackage.Trim().Replace('/', Path.DirectorySeparatorChar)));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return PackageResolution.Fail($"The package identity '{requestedPackage}' is not a valid package name.");
        }

        // Containment: a crafted identity must not escape node_modules through traversal.
        var containmentPrefix = normalizedNodeModules + Path.DirectorySeparatorChar;
        if (!requestedDirectory.StartsWith(containmentPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return PackageResolution.Fail($"The package identity '{requestedPackage}' escapes node_modules.");
        }

        // Enumerate every loadable Command Palette package so an ambiguous layout is rejected
        // rather than silently resolved to whichever package happens to sort first.
        var cmdpalPackages = new List<string>();
        foreach (var candidate in EnumeratePackageDirectories(nodeModules))
        {
            if (HasValidManifest(candidate))
            {
                cmdpalPackages.Add(Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate)));
            }
        }

        if (cmdpalPackages.Count == 0)
        {
            return PackageResolution.Fail("The installed npm package is not a Command Palette extension.");
        }

        if (cmdpalPackages.Count > 1)
        {
            return PackageResolution.Fail(
                $"The install contains {cmdpalPackages.Count} Command Palette packages under node_modules; refusing an ambiguous layout.");
        }

        var normalizedRequested = Path.TrimEndingDirectorySeparator(requestedDirectory);
        if (!string.Equals(cmdpalPackages[0], normalizedRequested, StringComparison.OrdinalIgnoreCase))
        {
            return PackageResolution.Fail(
                $"The requested package '{requestedPackage}' was not found as a Command Palette extension under node_modules.");
        }

        return PackageResolution.Ok(requestedDirectory);
    }

    /// <summary>
    /// Builds the discoverable layout for the resolved package inside the staging tree and
    /// returns the assembled directory, ready to be promoted atomically. The package becomes
    /// the extension root, and the dependencies npm hoisted to the top-level node_modules are
    /// moved under the package's own node_modules so Node.js can resolve them after promotion.
    /// </summary>
    /// <param name="installRoot">The staging directory that contains node_modules.</param>
    /// <param name="packageDirectory">The resolved package directory from <see cref="ResolveRequestedPackage"/>.</param>
    /// <returns>The assembled directory to promote.</returns>
    public static string AssembleDiscoveryLayout(string installRoot, string packageDirectory)
    {
        var assembledDirectory = Path.Combine(installRoot, AssembledDirectoryName);
        if (Directory.Exists(assembledDirectory))
        {
            Directory.Delete(assembledDirectory, recursive: true);
        }

        // Move the package itself to the assembled root.
        Directory.Move(packageDirectory, assembledDirectory);

        // Move every remaining hoisted dependency into the package's own node_modules.
        var topLevelNodeModules = Path.Combine(installRoot, NodeModulesName);
        if (Directory.Exists(topLevelNodeModules))
        {
            var assembledNodeModules = Path.Combine(assembledDirectory, NodeModulesName);
            Directory.CreateDirectory(assembledNodeModules);

            foreach (var entry in Directory.EnumerateFileSystemEntries(topLevelNodeModules))
            {
                var name = Path.GetFileName(entry);
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                var destination = Path.Combine(assembledNodeModules, name);
                if (Directory.Exists(entry))
                {
                    if (!Directory.Exists(destination))
                    {
                        Directory.Move(entry, destination);
                    }
                }
                else if (!File.Exists(destination))
                {
                    File.Move(entry, destination);
                }
            }
        }

        return assembledDirectory;
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
}
