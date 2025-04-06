// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.WindowsTerminal.Helpers;
using Microsoft.CmdPal.Ext.WindowsTerminal.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WindowsTerminal;

public partial class WindowsTerminalCommandsProvider : CommandProvider
{
    private readonly TerminalTopLevelCommandItem _terminalCommand;
    private readonly SettingsManager _settingsManager = new();

    public static IconInfo TerminalIcon { get; } = IconHelpers.FromRelativePath("Assets\\WindowsTerminal.svg");

    public WindowsTerminalCommandsProvider()
    {
        Id = "WindowsTerminalProfiles";
        DisplayName = Resources.extension_name;
        Icon = TerminalIcon;
        Settings = _settingsManager.Settings;

        _terminalCommand = new TerminalTopLevelCommandItem(_settingsManager)
        {
            MoreCommands = [
                new CommandContextItem(Settings.SettingsPage),
            ],
        };
    }

    public override ICommandItem[] TopLevelCommands() => [_terminalCommand];
}
