// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.IO;
using Microsoft.CmdPal.Ext.WindowWalker.Helpers;
using Microsoft.CmdPal.Ext.WindowWalker.Pages;
using Microsoft.CmdPal.Ext.WindowWalker.Properties;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace Microsoft.CmdPal.Ext.WindowWalker;

public partial class WalkerTopLevelCommandItem : CommandItem
{
    public WalkerTopLevelCommandItem(SettingsManager settingsManager)
        : base(new NoOpCommand())
    {
        Title = Resources.window_walker_top_level_command_title;
        Subtitle = Resources.wox_plugin_windowwalker_plugin_name;
        MoreCommands = [
            new CommandContextItem(new SettingsPage(settingsManager)),
        ];
    }
}
