// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Security;
using ManagedCommon;

namespace Microsoft.CmdPal.Ext.Apps.Utils;

internal static class PathHelpers
{
    /// <summary>
    /// Determines whether the given path is inside the specified directory.
    /// Directory comparison is performed in a directory-boundary-aware manner
    /// ("C:\\Windows\\System32Apps" does not match "C:\\Windows\\System32").
    /// </summary>
    internal static bool IsPathInsideDirectory(string path, string directory)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(directory))
        {
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var fullDir = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            // On Windows, path comparisons are case-insensitive; use OrdinalIgnoreCase for reliability.
            return fullPath.StartsWith(fullDir, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException || ex is IOException || ex is SecurityException)
        {
            Logger.LogError($"PathHelpers.IsPathInsideDirectory failed for path '{path}': {ex.Message}");
            return false;
        }
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

        var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        return IsPathInsideDirectory(path, systemRoot);
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
}
