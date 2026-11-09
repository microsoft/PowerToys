// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;
using Microsoft.CmdPal.Ext.ClipboardHistory.Pages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.ClipboardHistory;

public partial class ClipboardHistoryCommandsProvider : CommandProvider
{
    private readonly ListItem _clipboardHistoryListItem;
    private readonly ClipboardHistoryListPage _clipboardHistoryPage;
    private readonly SettingsManager _settingsManager = new();

    public ClipboardHistoryCommandsProvider()
    {
        _clipboardHistoryPage = new ClipboardHistoryListPage(_settingsManager);
        _clipboardHistoryListItem = new ListItem(_clipboardHistoryPage)
        {
            Title = Properties.Resources.list_item_title,
            Icon = Icons.ClipboardListIcon,
            MoreCommands = [
                new CommandContextItem(_settingsManager.Settings.SettingsPage),
            ],
        };
        DisplayName = Properties.Resources.provider_display_name;
        Icon = Icons.ClipboardListIcon;
        Id = "Windows.ClipboardHistory";

        Settings = _settingsManager.Settings;
    }

    public override IListItem[] TopLevelCommands()
    {
        return [_clipboardHistoryListItem];
    }

    public override void Dispose()
    {
        _clipboardHistoryPage.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
