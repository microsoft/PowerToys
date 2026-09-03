// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// Resolves an absolute path to the Node.js runtime (<c>node.exe</c>) by probing the
/// process PATH. Launching an explicit, validated absolute path rather than the bare
/// name <c>node</c> keeps <see cref="System.Diagnostics.Process"/> from resolving
/// <c>node.exe</c> out of the spawning process's working directory (which for a JS
/// extension is the extension's own, untrusted, folder) or via other implicit search
/// locations. It also lets the caller surface a specific "Node.js not found" error
/// instead of a generic Win32 launch failure.
/// </summary>
internal static class NodeRuntimeLocator
{
    private const string NodeExecutableName = "node.exe";

    /// <summary>
    /// Resolves <c>node.exe</c> from the current process PATH.
    /// </summary>
    /// <returns>The absolute path to <c>node.exe</c>, or <see langword="null"/> when it is not on PATH.</returns>
    internal static string? ResolveNodeExecutable() => ResolveNodeExecutable(GetPathDirectories());

    /// <summary>
    /// Resolves <c>node.exe</c> from an explicit ordered list of directories. Exposed for testing.
    /// </summary>
    /// <param name="pathDirectories">The directories to probe, in priority order.</param>
    /// <returns>The absolute path to the first existing <c>node.exe</c>, or <see langword="null"/>.</returns>
    internal static string? ResolveNodeExecutable(IReadOnlyList<string> pathDirectories)
    {
        ArgumentNullException.ThrowIfNull(pathDirectories);

        foreach (var directory in pathDirectories)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory))
            {
                continue;
            }

            try
            {
                var canonicalDirectory = Path.GetFullPath(directory);
                var candidate = Path.GetFullPath(Path.Combine(canonicalDirectory, NodeExecutableName));
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                // Malformed PATH entry; skip it.
                continue;
            }
        }

        return null;
    }

    private static IReadOnlyList<string> GetPathDirectories()
    {
        var pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVariable))
        {
            return [];
        }

        return pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
