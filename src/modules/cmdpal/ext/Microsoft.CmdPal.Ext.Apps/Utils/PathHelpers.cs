// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace Microsoft.CmdPal.Ext.Apps.Utils;

internal static class PathHelpers
{
    private static readonly string CachedSystemRoot =
        NormalizeDirectory(Environment.GetFolderPath(Environment.SpecialFolder.Windows));

    /// <summary>
    /// Determines whether the given path is inside the specified directory.
    /// Uses pure string comparison (no filesystem access). Directory comparison
    /// is boundary-aware ("C:\\Windows\\System32Apps" does not match "C:\\Windows\\System32").
    /// </summary>
    internal static bool IsPathInsideDirectory(string path, string directory)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(directory))
        {
            return false;
        }

        var normalizedPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedDir = NormalizeDirectory(directory);

        return normalizedPath.StartsWith(normalizedDir, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether the given path resides inside %SystemRoot% (e.g. C:\Windows).
    /// </summary>
    internal static bool IsSystemRootPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        var normalizedPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return normalizedPath.StartsWith(CachedSystemRoot, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether the given path is itself a shortcut (.lnk) file,
    /// indicating an unresolved shortcut chain where there is no real executable to uninstall.
    /// </summary>
    internal static bool IsShortcutFile(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        return path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Ensures a directory string ends with exactly one directory separator.
    /// </summary>
    private static string NormalizeDirectory(string directory)
    {
        return directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
    }
}
