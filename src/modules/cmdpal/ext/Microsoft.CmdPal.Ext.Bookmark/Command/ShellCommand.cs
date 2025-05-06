// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Bookmarks.Helpers;
using Microsoft.CmdPal.Ext.Bookmarks.Models;
using Microsoft.CmdPal.Ext.Bookmarks.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Bookmarks.Command;

public sealed partial class ShellCommand : InvokableCommand
{
    private string BookmarkName { get; }

    public string BookmarkValue { get; }

    public ShellCommand(BookmarkData data)
        : this(data.Name, data.Bookmark)
    {
    }

    public ShellCommand(string name, string value)
    {
        BookmarkName = name;
        BookmarkValue = value;
        Icon = IconHelper.CommandIcon;
        Name = name;
    }

    public override CommandResult Invoke()
    {
        return ShellCommand.Invoke(BookmarkValue);
    }

    public static CommandResult Invoke(string bookmarkValue)
    {
        // We assume all command bookmarks will follow the same format.
        // For example: "python test.py" or "pwsh test.ps1"
        // So, we can split the command and get the first part as the command name.
        var splittedBookmarkValue = bookmarkValue.Split(" ");
        if (splittedBookmarkValue.Length <= 1)
        {
            // show toast
            return CommandResult.ShowToast(new ToastArgs() { Message = Resources.bookmarks_command_invoke_failed_message });
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
