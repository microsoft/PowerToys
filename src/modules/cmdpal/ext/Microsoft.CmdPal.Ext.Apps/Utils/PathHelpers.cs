// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Microsoft.CmdPal.Ext.Apps.Utils;

internal static class PathHelpers
{
    private static readonly HashSet<string> ExecutableApplicationExtensions = new(StringComparer.OrdinalIgnoreCase) { "exe", "bat", "bin", "com", "cpl", "msc", "msi", "cmd", "ps1", "job", "msp", "mst", "sct", "ws", "wsh", "wsf" };

    private static readonly string CachedSystemRoot =
        NormalizeDirectory(Environment.GetFolderPath(Environment.SpecialFolder.Windows));

    private static readonly string[] AppSearchRoots = new[]
    {
        Environment.GetFolderPath(Environment.SpecialFolder.Programs),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
        Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps"),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    }
    .Where(Path.IsPathFullyQualified)
    .Select(path => NormalizeDirectory(NormalizePath(path)))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .OrderByDescending(path => path.Length)
    .ToArray();

    /// <summary>Removes shared discovery directories from ordinary application search terms.</summary>
    internal static string GetAppSearchPath(string path)
    {
        if (!Path.IsPathFullyQualified(path))
        {
            return path;
        }

        var normalized = NormalizePath(path);
        foreach (var root in AppSearchRoots)
        {
            if (normalized.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return normalized[root.Length..];
            }

            if (string.Equals(NormalizeDirectory(normalized), root, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }
        }

        return normalized;
    }

    /// <summary>
    /// Returns a full path without a trailing directory separator, or the original value when normalization fails.
    /// </summary>
    internal static string NormalizePath(string path)
    {
        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }
    }

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

    /// <summary>Determines whether a source path directly names a supported executable type.</summary>
    internal static bool IsExecutablePath(string path)
    {
        var extension = Path.GetExtension(path);
        return !string.IsNullOrEmpty(extension) && ExecutableApplicationExtensions.Contains(extension[1..]);
    }

    /// <summary>Classifies missing or syntactically invalid path failures that cannot be repaired by a short read retry.</summary>
    internal static bool IsMissingOrInvalidPath(Exception exception)
    {
        return exception is FileNotFoundException or DirectoryNotFoundException or ArgumentException or NotSupportedException
            || exception.HResult is unchecked((int)0x80070002) or unchecked((int)0x80070003)
                or unchecked((int)0x80030002) or unchecked((int)0x80030003) or unchecked((int)0x8007007B);
    }

    /// <summary>Classifies transient file or COM read failures, excluding missing paths, denied access, and offline networks.</summary>
    internal static bool IsRetryableReadFailure(Exception exception)
    {
        return (exception is IOException and not PathTooLongException
            && !IsMissingOrInvalidPath(exception)
            && exception.HResult is not (unchecked((int)0x80070005) or unchecked((int)0x80030005)
                or unchecked((int)0x80070035) or unchecked((int)0x80070040) or unchecked((int)0x80070043)
                or unchecked((int)0x800704C6) or unchecked((int)0x800704CF) or unchecked((int)0x800704D0)))
            || (exception is COMException && exception.HResult is unchecked((int)0x80070020) or unchecked((int)0x80070021)
                or unchecked((int)0x80030020) or unchecked((int)0x80030021));
    }
}
