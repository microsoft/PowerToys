// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.ClipboardHistory.Pages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.ClipboardHistory;

public partial class ClipboardHistoryCommandsProvider : CommandProvider
{
    private readonly ListItem _clipboardHistoryListItem;

    public ClipboardHistoryCommandsProvider()
    {
        _clipboardHistoryListItem = new ListItem(new ClipboardHistoryListPage())
        {
            Title = Properties.Resources.list_item_title,
            Subtitle = Properties.Resources.list_item_subtitle,
            Icon = new IconInfo("\xE8C8"), // Copy icon
        };

        DisplayName = Properties.Resources.provider_display_name;
        Icon = new IconInfo("\xE8C8"); // Copy icon
        Id = "Windows.ClipboardHistory";
    }

    public override IListItem[] TopLevelCommands()
    {
        return [_clipboardHistoryListItem];
    }
}
