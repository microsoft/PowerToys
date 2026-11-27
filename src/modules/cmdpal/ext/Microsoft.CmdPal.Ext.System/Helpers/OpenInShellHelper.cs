// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.System.Helpers;

public static partial class OpenInShellHelper
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

    public enum ShellRunAsType
    {
        None,
        Administrator,
        OtherUser,
    }
}
