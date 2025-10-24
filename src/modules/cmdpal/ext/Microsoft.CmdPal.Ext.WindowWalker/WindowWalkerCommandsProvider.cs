// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.WindowWalker.Helpers;
using Microsoft.CmdPal.Ext.WindowWalker.Pages;
using Microsoft.CmdPal.Ext.WindowWalker.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WindowWalker;

public partial class WindowWalkerCommandsProvider : CommandProvider
{
    private readonly CommandItem _windowWalkerPageItem;
    private readonly CommandItem _bandItem;
    private readonly SettingsManager _settings = SettingsManager.Instance;
    internal static readonly VirtualDesktopHelper VirtualDesktopHelperInstance = new();

    public WindowWalkerCommandsProvider()
    {
        _settings = new();
        Id = "WindowWalker";
        DisplayName = Resources.windowwalker_name;
        Icon = Icons.WindowWalkerIcon;
        Settings = _settings.Settings;

        _windowWalkerPageItem = new CommandItem(new WindowWalkerListPage(_settings))
        {
            Title = Resources.window_walker_top_level_command_title,
            Subtitle = Resources.windowwalker_name,
            MoreCommands = [
                new CommandContextItem(Settings.SettingsPage),
            ],
        };

        var testSettings = new SettingsManager();
        testSettings.HideExplorerSettingInfo = true;
        testSettings.InMruOrder = false;
        testSettings.ResultsFromVisibleDesktopOnly = true;
        testSettings.UseWindowIcon = true;
        var testPage = new WindowWalkerListPage(testSettings);
        testPage.Id = "com.microsoft.cmdpal.windowwalker.dockband";

        _bandItem = new CommandItem(testPage)
        {
            Title = Resources.window_walker_top_level_command_title,
            Subtitle = Resources.windowwalker_name,
            MoreCommands = [
                new CommandContextItem(Settings.SettingsPage),
            ],
        };
    }

    public override ICommandItem[] TopLevelCommands() => [_windowWalkerPageItem, _bandItem];
}
