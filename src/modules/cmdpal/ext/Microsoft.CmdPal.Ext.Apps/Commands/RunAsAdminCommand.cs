// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps.Properties;
using Microsoft.CmdPal.Ext.Apps.Utils;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps.Commands;

internal sealed partial class RunAsAdminCommand : InvokableCommand
{
    private readonly string _target;
    private readonly string _parentDir;
    private readonly bool _packaged;

    public RunAsAdminCommand(string target, string parentDir, bool packaged)
    {
        Name = Resources.run_as_administrator;
        Icon = Icons.RunAsAdminIcon;

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
                // For UWP/packaged apps, use shell:AppsFolder which works from packaged context
                var command = "shell:AppsFolder\\" + target;
                command = Environment.ExpandEnvironmentVariables(command.Trim());

                var info = ShellCommand.SetProcessStartInfo(command, verb: "runas");
                info.UseShellExecute = true;
                info.Arguments = string.Empty;
                Process.Start(info);
            }
            else
            {
                // For Win32 apps, use ActionRunner helper process to work around WinUI3/MSIX packaging limitation.
                // When running from a packaged app, ShellExecute with "runas" verb may fail for certain apps
                // (e.g., apps launched via .lnk shortcuts). ActionRunner runs outside the MSIX container,
                // so it can properly invoke the UAC dialog.
                var actionRunnerPath = ActionRunnerHelper.GetActionRunnerPath();

                if (string.IsNullOrEmpty(actionRunnerPath))
                {
                    // Fallback to direct Process.Start if ActionRunner is not found
                    ExtensionHost.LogMessage($"ActionRunner not found, falling back to direct Process.Start for '{target}'");
                    var info = ShellCommand.GetProcessStartInfo(target, parentDir, string.Empty, ShellCommand.RunAsType.Administrator);
                    Process.Start(info);
                    return;
                }

                var args = $"-run-as-admin -target \"{target}\"";
                if (!string.IsNullOrEmpty(parentDir))
                {
                    args += $" -workingDir \"{parentDir}\"";
                }

                var processInfo = new ProcessStartInfo
                {
                    FileName = actionRunnerPath,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                ExtensionHost.LogMessage($"Launching '{target}' as administrator via ActionRunner");
                Process.Start(processInfo);
            }
        });
    }

    public override CommandResult Invoke()
    {
        _ = RunAsAdmin(_target, _parentDir, _packaged).ConfigureAwait(false);

        return CommandResult.Dismiss();
    }
}
