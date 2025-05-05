// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.WindowsSettings.Classes;
using Microsoft.CmdPal.Ext.WindowsSettings.Helpers;
using Microsoft.CmdPal.Ext.WindowsSettings.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.ApplicationModel.DataTransfer;
using Windows.Networking.NetworkOperators;
using Windows.UI;

namespace Microsoft.CmdPal.Ext.WindowsSettings.Commands;

internal sealed partial class OpenSettingsCommand : InvokableCommand
{
    private readonly WindowsSetting _entry;

    internal OpenSettingsCommand(WindowsSetting entry)
    {
        Name = Resources.OpenSettings;
        Icon = new IconInfo("\xE8C8");
        _entry = entry;
    }

    private static bool DoOpenSettingsAction(WindowsSetting entry)
    {
        ProcessStartInfo processStartInfo;

        var command = entry.Command;

        if (command.Contains("%windir%", StringComparison.InvariantCultureIgnoreCase))
        {
            var windowsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            command = command.Replace("%windir%", windowsFolder, StringComparison.InvariantCultureIgnoreCase);
        }

        if (command.Contains(' '))
        {
            var commandSplit = command.Split(' ');
            var file = commandSplit.FirstOrDefault() ?? string.Empty;
            var arguments = command[file.Length..].TrimStart();

            processStartInfo = new ProcessStartInfo(file, arguments)
            {
                UseShellExecute = false,
            };
        }
        else
        {
            processStartInfo = new ProcessStartInfo(command)
            {
                UseShellExecute = true,
            };
        }

        try
        {
            Process.Start(processStartInfo);
            return true;
        }
#pragma warning disable CS0168, IDE0059
        catch (Exception exception)
        {
            // TODO GH #108 Logging is something we have to take care of
            // Log.Exception("can't open settings", exception, typeof(ResultHelper));
            return false;
        }
#pragma warning restore CS0168, IDE0059
    }

    public override CommandResult Invoke()
    {
        DoOpenSettingsAction(_entry);

        return CommandResult.Dismiss();
    }
}
