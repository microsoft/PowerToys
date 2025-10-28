// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;
using Microsoft.CmdPal.Ext.ClipboardHistory.Pages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation.Collections;

namespace Microsoft.CmdPal.Ext.ClipboardHistory;

public partial class ClipboardHistoryCommandsProvider : CommandProvider, IExtendedAttributesProvider
{
    private readonly CommandItem _bandItem;
    private readonly ListItem _clipboardHistoryListItem;
    private readonly SettingsManager _settingsManager = new();

    public ClipboardHistoryCommandsProvider()
    {
        var page = new ClipboardHistoryListPage(_settingsManager);
        _clipboardHistoryListItem = new ListItem(page)
        {
            Title = Properties.Resources.list_item_title,
            Subtitle = Properties.Resources.list_item_subtitle,
            Icon = Icons.ClipboardListIcon,
            MoreCommands = [
                new CommandContextItem(_settingsManager.Settings.SettingsPage),
            ],
        };
        _bandItem = new ClipboardHistoryBand(page);
        DisplayName = Properties.Resources.provider_display_name;
        Icon = Icons.ClipboardListIcon;
        Id = "Windows.ClipboardHistory";

        Settings = _settingsManager.Settings;
    }

    public override IListItem[] TopLevelCommands()
    {
        return [_clipboardHistoryListItem];
    }

    public IDictionary<string, object> GetProperties()
    {
        return new PropertySet()
        {
            { "DockBands", new ICommandItem[] { _bandItem } },
        };
    }
}
#pragma warning disable SA1402 // File may only contain a single type

internal sealed partial class ClipboardHistoryBand : CommandItem
{
    public ClipboardHistoryBand(ClipboardHistoryListPage page)
    {
        var item = new WrappedItem(page)
        {
            Icon = Icons.ClipboardListIcon,
        };
        Command = new WrappedDockList(item)
        {
            Icon = Icons.ClipboardListIcon,
            Name = string.Empty,
        };
        Title = string.Empty;
    }

    private sealed partial class WrappedItem : CommandItem
    {
        public override string Title => string.Empty;

        public WrappedItem(ICommand command)
        {
            Command = command;
        }
    }

    private sealed partial class WrappedDockList : ListPage
    {
        public override string Name => "TODO!";

        public override string Id => "com.microsoft.cmdpal.clipboardHistory.Band";

        private ICommandItem[] _items;

        public WrappedDockList(ICommandItem item)
        {
            _items = new ICommandItem[] { item };
            Name = string.Empty;
        }

        public override IListItem[] GetItems()
        {
            // TODO! We're using a space here, because if we use the empty string,
            // then CommandItemViewModel will fall back to the Command's name,
            // and we don't want to have to modify the page (since we're using the
            // same Page instance for the command and the band)
            // return _items.Select(i => new ListItem(i) { Title = " " }).ToArray();
            return _items.Select(i => new ListItem(i)).ToArray();
        }
    }
}
#pragma warning restore SA1402 // File may only contain a single type
