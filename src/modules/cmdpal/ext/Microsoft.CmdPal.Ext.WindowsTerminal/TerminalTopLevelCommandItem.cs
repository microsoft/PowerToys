// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.WindowsTerminal.Helpers;
using Microsoft.CmdPal.Ext.WindowsTerminal.Pages;
using Microsoft.CmdPal.Ext.WindowsTerminal.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WindowsTerminal;

public partial class TerminalTopLevelCommandItem : CommandItem
{
    public TerminalTopLevelCommandItem(SettingsManager settingsManager)
        : base(new ProfilesListPage(settingsManager))
    {
        Icon = WindowsTerminalCommandsProvider.TerminalIcon;
        Title = Resources.list_item_title;
    }
}
