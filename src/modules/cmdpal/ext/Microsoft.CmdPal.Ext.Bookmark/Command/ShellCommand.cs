// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CmdPal.Ext.Bookmarks.Helpers;
using Microsoft.CmdPal.Ext.Bookmarks.Models;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Bookmarks.Command;

public sealed partial class ShellCommand : InvokableCommand
{
    private static readonly Dictionary<BookmarkType, string> ExecutableFileName = new()
    {
        { Models.BookmarkType.Cmd, "cmd.exe" },
        { Models.BookmarkType.PWSH, "pwsh.exe" },
        { Models.BookmarkType.PowerShell, "powershell.exe" },
        { Models.BookmarkType.Python, "python.exe" },
        { Models.BookmarkType.Python3, "python3.exe" },
    };

    private Models.BookmarkType BookmarkType { get; }

    private string BookmarkName { get; }

    public string BookmarkValue { get; }

    public ShellCommand(BookmarkData data)
        : this(data.Name, data.Bookmark, data.Type)
    {
    }

    public ShellCommand(string name, string value, BookmarkType type)
    {
        BookmarkName = name;
        BookmarkType = type;
        BookmarkValue = value;
        Icon = IconHelper.GetIconByType(type);

        Name = name;

        // Icon = new IconInfo(IconFromUrl(Value, type));
    }

    public override CommandResult Invoke()
    {
        return ShellCommand.Invoke(BookmarkValue, BookmarkType);
    }

    public static CommandResult Invoke(string bookmarkValue, BookmarkType bookmarkType)
    {
        var exeFile = ExecutableFileName[bookmarkType];

        if (string.IsNullOrEmpty(exeFile))
        {
            return CommandResult.ShowToast(new ToastArgs() { Message = "invalid bookmark type" });
        }

        var fullPath = string.Empty;
        if (!EnvironmentsCache.Instance.TryGetExecutableFileFullPath(exeFile, out fullPath))
        {
            return CommandResult.ShowToast(new ToastArgs() { Message = "invalid fullPath" });
        }

        var args = bookmarkValue;

        if (bookmarkType == BookmarkType.Cmd)
        {
            args = $"/C {bookmarkValue}";
        }

        if (!OpenInShellHelper.OpenInShell(fullPath, args, null, OpenInShellHelper.ShellRunAsType.None, false))
        {
            ExtensionHost.LogMessage($"Failed to open {bookmarkValue} in shell.");
            return CommandResult.ShowToast(new ToastArgs() { Message = "Open in shell error." });
        }

        return CommandResult.Dismiss();
    }
}
