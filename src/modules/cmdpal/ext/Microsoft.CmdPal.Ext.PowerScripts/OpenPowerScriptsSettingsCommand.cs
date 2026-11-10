// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using ManagedCommon;
using Microsoft.CmdPal.Ext.PowerScripts.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.PowerScripts;

internal sealed partial class OpenPowerScriptsSettingsCommand : InvokableCommand
{
    internal OpenPowerScriptsSettingsCommand()
    {
        Id = "com.microsoft.cmdpal.builtin.powerscripts.settings";
        Name = Resources.OpenSettings;
        Icon = new IconInfo("\uE713");
    }

    public override ICommandResult Invoke()
    {
        try
        {
            var installDirectory = PowerToysPathResolver.GetPowerToysInstallPath();
            if (!string.IsNullOrEmpty(installDirectory))
            {
                var executable = Path.Combine(installDirectory, "PowerToys.exe");
                if (File.Exists(executable))
                {
                    var startInfo = new ProcessStartInfo(executable)
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    };
                    startInfo.ArgumentList.Add("--open-settings=PowerScripts");
                    using var process = Process.Start(startInfo);
                    if (process is not null)
                    {
                        return CommandResult.Dismiss();
                    }
                }
            }
        }
        catch (Exception)
        {
            // Report the launch failure to the user rather than silently dismissing the palette.
        }

        return CommandResult.ShowToast(Resources.SettingsUnavailable);
    }
}
