// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Peek.Common;
using Peek.Common.Models;

namespace Peek.FilePreviewer.Previewers;

public enum KnownSpecialFolder
{
    None,
    RecycleBin,
}

public static class KnownSpecialFolders
{
    private static readonly Lazy<IReadOnlyDictionary<string, KnownSpecialFolder>> FoldersByParsingNameDict = new(GetFoldersByParsingName);

    public static IReadOnlyDictionary<string, KnownSpecialFolder> FoldersByParsingName => FoldersByParsingNameDict.Value;

    private static Dictionary<string, KnownSpecialFolder> GetFoldersByParsingName()
    {
        var folders = new (KnownSpecialFolder Folder, string? ParsingName)[]
        {
            (KnownSpecialFolder.RecycleBin, GetParsingName("shell:RecycleBinFolder")),
        };

        return folders.Where(folder => !string.IsNullOrEmpty(folder.ParsingName))
                      .ToDictionary(folder => folder.ParsingName!, folder => folder.Folder);
    }

    private static string? GetParsingName(string shellName)
    {
        try
        {
            return CreateShellItemFromShellName(shellName)?.GetDisplayName(Windows.Win32.UI.Shell.SIGDN.SIGDN_DESKTOPABSOLUTEPARSING);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static IShellItem? CreateShellItemFromShellName(string shellName)
    {
        // Based on https://stackoverflow.com/a/42966899
        const string ShellItem = "43826d1e-e718-42ee-bc55-a1e261c37bfe";

        Guid shellItem2Guid = new(ShellItem);
        int retCode = NativeMethods.SHCreateItemFromParsingName(shellName, IntPtr.Zero, ref shellItem2Guid, out IShellItem? nativeShellItem);

        return retCode == 0 ? nativeShellItem : null;
    }
}
