// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Indexer.Indexer.Utils;
using Microsoft.CmdPal.Ext.Indexer.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Indexer.Commands;

// This Command is a copy of Microsoft.CmdPal.Ext.Apps.Commands.RunAsUserCommand
// Should this stay as a copy or be moved to a reference?
// Thats different built-in extensions so im unsure of the best practice here.
internal sealed partial class RunAsUserCommand : InvokableCommand
{
    private readonly string _target;
    private readonly string _parentDir;

    public RunAsUserCommand(string target, string parentDir)
    {
        Name = Resources.Indexer_Command_RunAsDifferentUser;
        Icon = Icons.RunAsUserIcon;

        _target = target;
        _parentDir = parentDir;
    }

    internal static async Task RunAsUser(string target, string parentDir)
    {
        await Task.Run(() =>
        {
            var info = ShellCommand.GetProcessStartInfo(target, parentDir, string.Empty, ShellCommand.RunAsType.OtherUser);

            Process.Start(info);
        });
    }

    public override CommandResult Invoke()
    {
        _ = RunAsUser(_target, _parentDir).ConfigureAwait(false);

        return CommandResult.Dismiss();
    }
}
