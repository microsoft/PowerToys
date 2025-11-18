// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using Windows.Win32;
using Windows.Win32.Storage.FileSystem;

namespace Microsoft.CmdPal.Core.Common.Helpers;

public static class PathHelper
{
    public static bool Exists(string path, out bool isDirectory)
    {
        isDirectory = false;
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        string? fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return false;
        }

        var result = ExistsCore(fullPath, out isDirectory);
        if (result && IsDirectorySeparator(fullPath[^1]))
        {
            // Some sys-calls remove all trailing slashes and may give false positives for existing files.
            // We want to make sure that if the path ends in a trailing slash, it's truly a directory.
            return isDirectory;
        }

        return result;
    }

    /// <summary>
    /// Normalize potential local/UNC file path text input: trim whitespace and surrounding quotes.
    /// Windows file paths cannot contain quotes, but user input can include them.
    /// </summary>
    public static string Unquote(string? text)
    {
        return string.IsNullOrWhiteSpace(text) ? (text ?? string.Empty) : text.Trim().Trim('"');
    }

    /// <summary>
    /// Quick heuristic to determine if the string looks like a Windows file path (UNC or drive-letter based).
    /// </summary>
    public static bool LooksLikeFilePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        // UNC path
        if (path.StartsWith(@"\\", StringComparison.Ordinal))
        {
            // Win32 File Namespaces \\?\
            if (path.StartsWith(@"\\?\", StringComparison.Ordinal))
            {
                return IsSlow(path[4..]);
            }

            // Basic UNC path validation: \\server\share or \\server\share\path
            var parts = path[2..].Split('\\', StringSplitOptions.RemoveEmptyEntries);

            return parts.Length >= 2; // At minimum: server and share
        }

        // Drive letter path (e.g., C:\ or C:)
        return path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':';
    }

    /// <summary>
    /// Validates path syntax without performing any I/O by using Path.GetFullPath.
    /// </summary>
    public static bool HasValidPathSyntax(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            _ = Path.GetFullPath(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if a string represents a valid Windows file path (local or network)
    /// using fast syntax validation only. Reuses LooksLikeFilePath and HasValidPathSyntax.
    /// </summary>
    public static bool IsValidFilePath(string? path)
    {
        return LooksLikeFilePath(path) && HasValidPathSyntax(path);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsDirectorySeparator(char c)
    {
        return c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar;
    }

    private static bool ExistsCore(string fullPath, out bool isDirectory)
    {
        var attributes = PInvoke.GetFileAttributes(fullPath);
        var result = attributes != PInvoke.INVALID_FILE_ATTRIBUTES;
        isDirectory = result && (attributes & (uint)FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_DIRECTORY) != 0;
        return result;
    }

    public static bool IsSlow(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        try
        {
            var root = Path.GetPathRoot(path);
            if (!string.IsNullOrEmpty(root))
            {
                if (root.Length > 2 && char.IsLetter(root[0]) && root[1] == ':')
                {
                    return new DriveInfo(root).DriveType is not (DriveType.Fixed or DriveType.Ram);
                }
                else if (root.StartsWith(@"\\", StringComparison.Ordinal))
                {
                    return !root.StartsWith(@"\\?\", StringComparison.Ordinal) || IsSlow(root[4..]);
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}
