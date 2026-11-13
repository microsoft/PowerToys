// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.WindowsSettings.Classes;
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
        Icon = Icons.CopyIcon;
        _entry = entry;
    }

    private static bool DoOpenSettingsAction(WindowsSetting entry)
    {
        var command = entry.Command;
        var arguments = string.Empty;

        // Control Panel task items enumerated from the shell have no
        // executable command line and cannot be re-parsed by name — they are
        // launched from the shell id list captured during enumeration.
        if (entry.TaskIdList is { Length: > 0 })
        {
            return Helpers.ShellInterop.InvokeShellItemByIdList(entry.TaskIdList);
        }

        if (command.Contains("%windir%", StringComparison.InvariantCultureIgnoreCase))
        {
            var windowsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            command = command.Replace("%windir%", windowsFolder, StringComparison.InvariantCultureIgnoreCase);
        }

        if (command.Contains(' '))
        {
            var commandSplit = command.Split(' ');
            var file = commandSplit.FirstOrDefault() ?? string.Empty;
            arguments = command[file.Length..].TrimStart();
            command = file;
        }

        try
        {
            ShellHelpers.OpenInShellOrThrow(command, arguments);
            return true;
        }
        catch (Exception exception)
        {
            ExtensionHost.LogMessage($"Unable to open {command}: {exception}");
            return false;
        }
    }

    public override CommandResult Invoke()
    {
        DoOpenSettingsAction(_entry);

        return CommandResult.Dismiss();
    }
}
