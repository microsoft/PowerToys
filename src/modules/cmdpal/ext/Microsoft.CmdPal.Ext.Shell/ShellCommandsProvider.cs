// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Shell.Helpers;
using Microsoft.CmdPal.Ext.Shell.Pages;
using Microsoft.CmdPal.Ext.Shell.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Shell;

public partial class ShellCommandsProvider : CommandProvider
{
    private readonly CommandItem _shellPageItem;
    private readonly SettingsManager _settingsManager = new();
    private readonly FallbackCommandItem _fallbackItem;

    public ShellCommandsProvider()
    {
        Id = "Run";
        DisplayName = Resources.cmd_plugin_name;
        Icon = Icons.RunV2;
        Settings = _settingsManager.Settings;

        _fallbackItem = new FallbackExecuteItem(_settingsManager);

        _shellPageItem = new CommandItem(new ShellListPage(_settingsManager))
        {
            Icon = Icons.RunV2,
            Title = Resources.shell_command_name,
            Subtitle = Resources.cmd_plugin_description,
            MoreCommands = [
                new CommandContextItem(Settings.SettingsPage),
            ],
        };
    }

    public override ICommandItem[] TopLevelCommands() => [_shellPageItem];

    public override IFallbackCommandItem[]? FallbackCommands() => [_fallbackItem];
}
