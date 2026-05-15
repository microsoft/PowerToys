// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using ManagedCommon;
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
            try
            {
                var info = ShellCommand.GetProcessStartInfo(target, parentDir, string.Empty, ShellCommand.RunAsType.OtherUser);

                Process.Start(info);
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                // ERROR_CANCELLED: user dismissed the UAC/credential prompt — not an error
                Logger.LogDebug("Run as different user cancelled by user.");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to run as different user", ex);
            }
        });
    }

    public override CommandResult Invoke()
    {
        _ = RunAsUser(_target, _parentDir).ConfigureAwait(false);

        return CommandResult.Dismiss();
    }
}
