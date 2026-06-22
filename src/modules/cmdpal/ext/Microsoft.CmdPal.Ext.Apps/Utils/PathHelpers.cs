// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

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
            var fullPath = System.IO.Path.GetFullPath(path).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            var fullDir = System.IO.Path.GetFullPath(directory).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;

            // On Windows, path comparisons are case-insensitive; use OrdinalIgnoreCase for reliability.
            return fullPath.StartsWith(fullDir, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
