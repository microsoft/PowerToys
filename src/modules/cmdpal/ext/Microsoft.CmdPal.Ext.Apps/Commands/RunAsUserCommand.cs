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

internal sealed partial class RunAsUserCommand : InvokableCommand
{
    private readonly string _target;
    private readonly string _parentDir;

    public RunAsUserCommand(string target, string parentDir)
    {
        Name = Resources.run_as_different_user;
        Icon = Icons.RunAsUserIcon;

        _target = target;
        _parentDir = parentDir;
    }

    internal static async Task RunAsUser(string target, string parentDir)
    {
        await Task.Run(() =>
        {
            // Use ActionRunner helper process to work around WinUI3/MSIX packaging limitation.
            // When running from a packaged app, ShellExecute with "runasuser" verb causes
            // CredentialUIBroker.exe to spawn infinitely without showing the credential dialog.
            // ActionRunner runs outside the MSIX container, so it can properly invoke the credential UI.
            var actionRunnerPath = ActionRunnerHelper.GetActionRunnerPath();

            if (string.IsNullOrEmpty(actionRunnerPath))
            {
                // Fallback to direct Process.Start if ActionRunner is not found
                // This may not work in packaged context, but provides a fallback for development
                ExtensionHost.LogMessage($"ActionRunner not found, falling back to direct Process.Start for '{target}'");
                var info = ShellCommand.GetProcessStartInfo(target, parentDir, string.Empty, ShellCommand.RunAsType.OtherUser);
                Process.Start(info);
                return;
            }

            var args = $"-run-as-user -target \"{target}\"";
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

            ExtensionHost.LogMessage($"Launching '{target}' as different user via ActionRunner");
            Process.Start(processInfo);
        });
    }

    public override CommandResult Invoke()
    {
        _ = RunAsUser(_target, _parentDir).ConfigureAwait(false);

        return CommandResult.Dismiss();
    }
}
