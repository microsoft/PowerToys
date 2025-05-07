// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using Microsoft.CmdPal.Ext.Bookmarks.Helpers;
using Microsoft.CmdPal.Ext.Bookmarks.Models;
using Microsoft.CmdPal.Ext.Bookmarks.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Bookmarks.Command;

public sealed partial class ShellCommand : InvokableCommand
{
    public BookmarkData BookmarkData { get; }

    public ShellCommand(BookmarkData data)
    {
        BookmarkData = data;
        Name = data.Name;
        Icon = IconHelper.CreateIcon(data.Bookmark, data.Type, false);
    }

    public override CommandResult Invoke()
    {
        return ShellCommand.Invoke(BookmarkData.Type, BookmarkData.Bookmark);
    }

    public static CommandResult Invoke(BookmarkType bookmarkType, string bookmarkValue)
    {
        // if it's a file or folder bookmark, call them directly.
        if (bookmarkType == BookmarkType.File || bookmarkType == BookmarkType.Folder)
        {
            if (!OpenInShellHelper.OpenInShell(bookmarkValue, null, null, OpenInShellHelper.ShellRunAsType.None, false, out var errMsg))
            {
                ExtensionHost.LogMessage($"Failed to open {bookmarkValue} in shell. Ex: {errMsg}");
                return CommandResult.ShowToast(new ToastArgs() { Message = Resources.bookmarks_command_invoke_failed_message });
            }

            return CommandResult.Dismiss();
        }

        // We assume all command bookmarks will follow the same format.
        // For example: "python test.py" or "pwsh test.ps1"
        // So, we can split the command and get the first part as the command name.
        var splittedBookmarkValue = bookmarkValue.Split(" ");
        if (splittedBookmarkValue.Length <= 1)
        {
            // directly call. Because it maybe a command with no args. eg: haproxy.exe or cmd.exe
            if (!OpenInShellHelper.OpenInShell(splittedBookmarkValue[0], null, null, OpenInShellHelper.ShellRunAsType.None, false, out var errMsg))
            {
                ExtensionHost.LogMessage($"Failed to open {bookmarkValue} in shell. Ex: {errMsg}");
                return CommandResult.ShowToast(new ToastArgs() { Message = Resources.bookmarks_command_invoke_failed_message });
            }
        }

        // args = without the first part and join with space
        var args = splittedBookmarkValue[1..];

        if (!OpenInShellHelper.OpenInShell(splittedBookmarkValue[0], string.Join(" ", args), null, OpenInShellHelper.ShellRunAsType.None, false, out var errorMessage))
        {
            ExtensionHost.LogMessage($"Failed to open {bookmarkValue} in shell. Ex: {errorMessage}");
            return CommandResult.ShowToast(new ToastArgs() { Message = Resources.bookmarks_command_invoke_failed_message });
        }

        return CommandResult.Dismiss();
    }
}
