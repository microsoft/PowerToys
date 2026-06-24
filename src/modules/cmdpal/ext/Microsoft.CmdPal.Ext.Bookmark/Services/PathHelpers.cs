// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Bookmarks.Helpers;

namespace Microsoft.CmdPal.Ext.Bookmarks.Services;

internal static class PathHelpers
{
    /// <summary>
    /// Finds the nearest existing parent directory for a given path string.
    /// Normalizes the input (NFC) before probing and trims long-path prefixes on Windows.
    /// </summary>
    public static bool TryGetNearestExistingParentDirectory(string path, out string parentDirectory)
    {
        parentDirectory = string.Empty;

        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            var current = PathNormalization.NormalizePathForWindows(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            while (!string.IsNullOrWhiteSpace(current))
            {
                if (Directory.Exists(current))
                {
                    parentDirectory = PathNormalization.NormalizePathForWindows(Path.GetFullPath(current));
                    return true;
                }

                var next = Path.GetDirectoryName(current);
                if (string.IsNullOrEmpty(next) || next.Equals(current, StringComparison.Ordinal))
                {
                    break;
                }

                current = next;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to resolve nearest parent directory for '{path}'", ex);
        }

        return false;
    }

    /// <summary>
    /// Overload that accepts a Classification and probes classification.FileSystemTarget if present,
    /// otherwise classification.Target.
    /// </summary>
    public static bool TryGetNearestExistingParentDirectory(Classification classification, out string parentDirectory)
    {
        parentDirectory = string.Empty;

        try
        {
            var pathToProbe = string.IsNullOrWhiteSpace(classification.FileSystemTarget) ? classification.Target : classification.FileSystemTarget;
            if (string.IsNullOrWhiteSpace(pathToProbe))
            {
                return false;
            }

            return TryGetNearestExistingParentDirectory(pathToProbe, out parentDirectory);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to resolve fallback folder for '{classification.Target}'", ex);
            return false;
        }
    }
}
