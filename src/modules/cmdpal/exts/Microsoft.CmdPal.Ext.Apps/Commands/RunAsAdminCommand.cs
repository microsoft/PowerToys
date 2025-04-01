// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps.Properties;
using Microsoft.CmdPal.Ext.Apps.Utils;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps.Commands;

internal sealed partial class RunAsAdminCommand : InvokableCommand
{
    private static readonly IconInfo TheIcon = new("\uE7EF");

    private readonly string _target;
    private readonly string _parentDir;
    private readonly bool _packaged;

    public RunAsAdminCommand(string target, string parentDir, bool packaged)
    {
        Name = Resources.run_as_administrator;
        Icon = TheIcon;

        _target = target;
        _parentDir = parentDir;
        _packaged = packaged;
    }

    internal static async Task RunAsAdmin(string target, string parentDir, bool packaged)
    {
        await Task.Run(() =>
        {
            if (packaged)
            {
                var command = "shell:AppsFolder\\" + target;
                command = Environment.ExpandEnvironmentVariables(command.Trim());

                var info = ShellCommand.SetProcessStartInfo(command, verb: "runas");
                info.UseShellExecute = true;
                info.Arguments = string.Empty;
                Process.Start(info);
            }
            else
            {
                var info = ShellCommand.GetProcessStartInfo(target, parentDir, string.Empty, ShellCommand.RunAsType.Administrator);

                Process.Start(info);
            }
        });
    }

    public override CommandResult Invoke()
    {
        _ = RunAsAdmin(_target, _parentDir, _packaged).ConfigureAwait(false);

        return CommandResult.Dismiss();
    }
}
