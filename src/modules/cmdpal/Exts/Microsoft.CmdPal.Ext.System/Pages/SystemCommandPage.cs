// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.System.Helpers;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.System.Pages;

public sealed partial class SystemCommandPage : ListPage
{
    private SettingsManager _settingsManager;

    public SystemCommandPage(SettingsManager settingsManager)
    {
        Title = Resources.Microsoft_plugin_ext_system_page_name;
        Icon = new IconInfo("\uE72E");
        _settingsManager = settingsManager;
        ShowDetails = true;
    }

    public override IListItem[] GetItems() => Commands.GetAllCommands(_settingsManager).ToArray();
}
