// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.CmdPal.Ext.WindowsTerminal.Helpers;
using Microsoft.CmdPal.Ext.WindowsTerminal.Pages;
using Microsoft.CmdPal.Ext.WindowsTerminal.Properties;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace Microsoft.CmdPal.Ext.WindowsTerminal;

public partial class WindowsTerminalCommandsProvider : CommandProvider
{
    private readonly TerminalTopLevelCommandItem _terminalCommand;
    private readonly SettingsManager _settingsManager = new();

    public WindowsTerminalCommandsProvider()
    {
        Id = "WindowsTerminalProfiles";
        DisplayName = Resources.extension_name;
        Icon = new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory.ToString(), "Images\\WindowsTerminal.dark.png"));

        _terminalCommand = new TerminalTopLevelCommandItem(_settingsManager)
        {
            MoreCommands = [new CommandContextItem(new SettingsPage(_settingsManager))],
        };
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return [_terminalCommand];
    }
}
