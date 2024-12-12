// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.CmdPal.Ext.WindowWalker.Components;
using Microsoft.CmdPal.Ext.WindowWalker.Helpers;
using Microsoft.CmdPal.Ext.WindowWalker.Pages;
using Microsoft.CmdPal.Ext.WindowWalker.Properties;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace Microsoft.CmdPal.Ext.WindowWalker;

public partial class WindowWalkerCommandsProvider : CommandProvider
{
    private readonly SettingsManager _settingsManager = new();

    internal static readonly VirtualDesktopHelper VirtualDesktopHelperInstance = new();

    public WindowWalkerCommandsProvider()
    {
        DisplayName = Resources.windowwalker_name;
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return [
            new CommandItem(new WindowWalkerListPage())
            {
                Title = Resources.window_walker_top_level_command_title,
                Subtitle = Resources.windowwalker_name,
                MoreCommands = [
                    new CommandContextItem(new SettingsPage()),
                ],
            },
        ];
    }
}
