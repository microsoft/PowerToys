// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using Microsoft.CmdPal.Ext.WindowsTerminal.Commands;
using Microsoft.CmdPal.Ext.WindowsTerminal.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WindowsTerminal;

public partial class WindowsTerminalCommandsProvider : CommandProvider
{
    private readonly TerminalTopLevelCommandItem _terminalCommand;
    private readonly SettingsManager _settingsManager = new();
    private readonly AppSettingsManager _appSettingsManager = new();

    public WindowsTerminalCommandsProvider()
    {
        Id = "WindowsTerminalProfiles";
        DisplayName = Resources.extension_name;
        Icon = Icons.TerminalIcon;
        Settings = _settingsManager.Settings;

        _terminalCommand = new TerminalTopLevelCommandItem(_settingsManager, _appSettingsManager)
        {
            MoreCommands = [
                new CommandContextItem(Settings.SettingsPage),
            ],
        };
    }

    public override ICommandItem[] TopLevelCommands() => [_terminalCommand];

    public override ICommandItem? GetCommandItem(string id)
    {
        var items = _terminalCommand.Command is Pages.ProfilesListPage page ? page.GetItems() : [];
        foreach (var item in items)
        {
            if (item.Command.Id == id)
            {
                return item;
            }
        }

        return null;
    }
}
