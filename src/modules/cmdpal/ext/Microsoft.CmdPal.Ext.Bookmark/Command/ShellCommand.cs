// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.CmdPal.Ext.Bookmarks.Helpers;
using Microsoft.CmdPal.Ext.Bookmarks.Models;
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
        Icon = IconHelper.PowerShellIcon;
        Name = name;
    }

    public override CommandResult Invoke()
    {
        return ShellCommand.Invoke(BookmarkValue);
    }

    public static CommandResult Invoke(string bookmarkValue)
    {
        if (!OpenInShellHelper.OpenInShell(fullPath, args, null, OpenInShellHelper.ShellRunAsType.None, false, out var errorMessage))
        {
            ExtensionHost.LogMessage($"Failed to open {bookmarkValue} in shell. Ex: {errorMessage}");
            return CommandResult.ShowToast(new ToastArgs() { Message = $"Open in shell error. Ex: {errorMessage}" });
        }

        return CommandResult.Dismiss();
    }
}
