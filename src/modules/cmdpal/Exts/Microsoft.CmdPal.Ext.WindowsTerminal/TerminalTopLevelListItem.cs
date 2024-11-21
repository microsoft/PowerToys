// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.IO;
using Microsoft.CmdPal.Ext.WindowsTerminal.Helpers;
using Microsoft.CmdPal.Ext.WindowsTerminal.Pages;
using Microsoft.CmdPal.Ext.WindowsTerminal.Properties;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace Microsoft.CmdPal.Ext.WindowsTerminal;

public partial class TerminalTopLevelListItem : ListItem
{
    public TerminalTopLevelListItem(SettingsManager settingsManager)
        : base(new ProfilesListPage(settingsManager.GetSettings()))
    {
        Icon = new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory.ToString(), "Images\\WindowsTerminal.dark.png"));
        Title = Resources.list_item_title;
    }
}
