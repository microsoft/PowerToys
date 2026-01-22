// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Microsoft.CmdPal.Ext.Bookmarks.Helpers;

/// <summary>
/// Helpers for getting user-friendly shell names and paths.
/// </summary>
internal static class ShellNames
{
    /// <summary>
    /// Tries to get a localized friendly name (e.g. "This PC", "Downloads") for a shell path like:
    ///  - "shell:Downloads"
    ///  - "shell:::{20D04FE0-3AEA-1069-A2D8-08002B30309D}"
    ///  - "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}"
    /// </summary>
    public static bool TryGetFriendlyName(string shellPath, [NotNullWhen(true)] out string? displayName)
    {
        displayName = null;

        // Normalize a bare GUID to the "::" moniker if someone passes only "{GUID}"
        if (shellPath.Length > 0 && shellPath[0] == '{' && shellPath[^1] == '}')
        {
            shellPath = "::" + shellPath;
        }

        nint pidl = 0;
        try
        {
            var hr = NativeMethods.SHParseDisplayName(shellPath, 0, out pidl, 0, 0);
            if (hr != 0 || pidl == 0)
            {
                return false;
            }

            // Ask for the human-friendly localized name
            nint psz;
            hr = NativeMethods.SHGetNameFromIDList(pidl, NativeMethods.SIGDN.NORMALDISPLAY, out psz);
            if (hr != 0 || psz == 0)
            {
                return false;
            }

            try
            {
                displayName = Marshal.PtrToStringUni(psz);
                return !string.IsNullOrWhiteSpace(displayName);
            }
            finally
            {
                NativeMethods.CoTaskMemFree(psz);
            }
        }
        finally
        {
            if (pidl != 0)
            {
                NativeMethods.CoTaskMemFree(pidl);
            }
        }
    }

    /// <summary>
    /// Optionally, also try to obtain a filesystem path (if the item represents one).
    /// Returns false for purely virtual items like "This PC".
    /// </summary>
    public static bool TryGetFileSystemPath(string shellPath, [NotNullWhen(true)] out string? fileSystemPath)
    {
        fileSystemPath = null;

        nint pidl = 0;
        try
        {
            var hr = NativeMethods.SHParseDisplayName(shellPath, 0, out pidl, 0, 0);
            if (hr != 0 || pidl == 0)
            {
                return false;
            }

            nint psz;
            hr = NativeMethods.SHGetNameFromIDList(pidl, NativeMethods.SIGDN.FILESYSPATH, out psz);
            if (hr != 0 || psz == 0)
            {
                return false;
            }

            try
            {
                fileSystemPath = Marshal.PtrToStringUni(psz);
                return !string.IsNullOrWhiteSpace(fileSystemPath);
            }
            finally
            {
                NativeMethods.CoTaskMemFree(psz);
            }
        }
        finally
        {
            if (pidl != 0)
            {
                NativeMethods.CoTaskMemFree(pidl);
            }
        }
    }
}
