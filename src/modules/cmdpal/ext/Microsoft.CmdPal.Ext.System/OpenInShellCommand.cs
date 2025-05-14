// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.System.Helpers;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.System;

public sealed partial class OpenInShellCommand : InvokableCommand
{
    public OpenInShellCommand(string name, string path, string? arguments = null, string? workingDir = null, OpenInShellHelper.ShellRunAsType runAs = OpenInShellHelper.ShellRunAsType.None, bool runWithHiddenWindow = false)
    {
        Name = name;
        _path = path;
        _arguments = arguments;
        _workingDir = workingDir;
        _runAs = runAs;
        _runWithHiddenWindow = runWithHiddenWindow;
    }

    public override CommandResult Invoke()
    {
        OpenInShellHelper.OpenInShell(_path, _arguments);
        return CommandResult.Dismiss();
    }

    private string _path;
    private string? _workingDir;
    private string? _arguments;
    private OpenInShellHelper.ShellRunAsType _runAs;
    private bool _runWithHiddenWindow;
}
