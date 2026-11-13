// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Ext.WindowWalker.Commands;

internal sealed partial class ExplorerInfoResultCommand : InvokableCommand
{
    public static bool OpenInShell(string path, string? arguments = null, string? workingDir = null, ShellRunAsType runAs = ShellRunAsType.None, bool runWithHiddenWindow = false)
    {
        var shellRunAs = runAs switch
        {
            ShellRunAsType.Administrator => ShellHelpers.ShellRunAsType.Administrator,
            ShellRunAsType.OtherUser => ShellHelpers.ShellRunAsType.OtherUser,
            _ => ShellHelpers.ShellRunAsType.None,
        };
        return ShellHelpers.OpenInShell(path, arguments, workingDir, shellRunAs, runWithHiddenWindow);
    }

    public override ICommandResult Invoke()
    {
        OpenInShell("rundll32.exe", "shell32.dll,Options_RunDLL 7"); // "shell32.dll,Options_RunDLL 7" opens the view tab in folder options of explorer.
        return CommandResult.Dismiss();
    }

    public enum ShellRunAsType
    {
        None,
        Administrator,
        OtherUser,
    }
}
