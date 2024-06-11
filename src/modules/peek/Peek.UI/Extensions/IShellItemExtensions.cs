// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using ManagedCommon;
using Peek.Common.Models;

namespace Peek.UI.Extensions;

public static class IShellItemExtensions
{
    public static IFileSystemItem ToIFileSystemItem(this IShellItem shellItem)
    {
        string path = shellItem.GetPath();
        string name = shellItem.GetName();

        return File.Exists(path) ? new FileItem(path, name) : new FolderItem(path, name, shellItem.GetParsingName());
    }

    private static string GetPath(this IShellItem shellItem) =>
        shellItem.GetNameCore(Windows.Win32.UI.Shell.SIGDN.SIGDN_FILESYSPATH, logError: false);

    private static string GetName(this IShellItem shellItem) =>
        shellItem.GetNameCore(Windows.Win32.UI.Shell.SIGDN.SIGDN_NORMALDISPLAY, logError: true);

    private static string GetParsingName(this IShellItem shellItem) =>
        shellItem.GetNameCore(Windows.Win32.UI.Shell.SIGDN.SIGDN_DESKTOPABSOLUTEPARSING, logError: true);

    private static string GetNameCore(this IShellItem shellItem, Windows.Win32.UI.Shell.SIGDN displayNameType, bool logError)
    {
        try
        {
            return shellItem.GetDisplayName(displayNameType);
        }
        catch (Exception ex)
        {
            if (logError)
            {
                Logger.LogError($"Getting {Enum.GetName(displayNameType)} failed. {ex.Message}");
            }

            return string.Empty;
        }
    }
}
