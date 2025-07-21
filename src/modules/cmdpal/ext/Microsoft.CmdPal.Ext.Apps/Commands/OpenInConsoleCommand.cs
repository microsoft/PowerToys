// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Apps.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps.Commands;

internal sealed partial class OpenInConsoleCommand : InvokableCommand
{
    private readonly string _target;

    public OpenInConsoleCommand(string target)
    {
        Name = Resources.open_path_in_console;
        Icon = Icons.OpenPathIcon;

        _target = target;
    }

    internal static async Task LaunchTarget(string t)
    {
        await Task.Run(() =>
        {
            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    WorkingDirectory = t,
                    FileName = "cmd.exe",
                };

                Process.Start(processStartInfo);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message);
            }
        });
    }

    public override CommandResult Invoke()
    {
        return CommandResult.Dismiss();
    }
}
