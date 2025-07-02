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

    internal static readonly VirtualDesktopHelper VirtualDesktopHelperInstance = new();

    public WindowWalkerCommandsProvider()
    {
        Id = "WindowWalker";
        DisplayName = Resources.windowwalker_name;
        Icon = IconHelpers.FromRelativePath("Assets\\WindowWalker.svg");
        Settings = SettingsManager.Instance.Settings;

        _windowWalkerPageItem = new CommandItem(new WindowWalkerListPage())
        {
            Title = Resources.window_walker_top_level_command_title,
            Subtitle = Resources.windowwalker_name,
            MoreCommands = [
                new CommandContextItem(Settings.SettingsPage),
            ],
        };
    }

    public override ICommandItem[] TopLevelCommands() => [_windowWalkerPageItem];
}
