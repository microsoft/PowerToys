// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CmdPal.Ext.WebSearch.Commands;
using Microsoft.CmdPal.Ext.WebSearch.Helpers;
using Microsoft.CmdPal.Ext.WebSearch.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using BrowserInfo = Microsoft.CmdPal.Ext.WebSearch.Helpers.DefaultBrowserInfo;

namespace Microsoft.CmdPal.Ext.WebSearch.Pages;

internal sealed partial class WebSearchListPage : DynamicListPage, IDisposable
{
    private readonly IconInfo _newSearchIcon = new(string.Empty);
    private readonly ISettingsInterface _settingsManager;
    private readonly Lock _sync = new();
    private static readonly CompositeFormat PluginInBrowserName = System.Text.CompositeFormat.Parse(Properties.Resources.plugin_in_browser_name);
    private static readonly CompositeFormat PluginOpen = System.Text.CompositeFormat.Parse(Properties.Resources.plugin_open);
    private IListItem[] _allItems = [];
    private List<ListItem> _historyItems = [];

    public WebSearchListPage(ISettingsInterface settingsManager)
    {
        Name = Resources.command_item_title;
        Title = Resources.command_item_title;
        Icon = IconHelpers.FromRelativePath("Assets\\WebSearch.png");
        Id = "com.microsoft.cmdpal.websearch";
        _settingsManager = settingsManager;
        _settingsManager.HistoryChanged += SettingsManagerOnHistoryChanged;

        // It just looks viewer to have string twice on the page, and default placeholder is good enough
        PlaceholderText = _allItems.Length > 0 ? Resources.plugin_description : string.Empty;

        EmptyContent = new CommandItem(new NoOpCommand())
        {
            Icon = Icon,
            Title = Properties.Resources.plugin_description,
            Subtitle = string.Format(CultureInfo.CurrentCulture, PluginInBrowserName, BrowserInfo.Name ?? BrowserInfo.MSEdgeName),
        };

        UpdateHistory();
        RequeryAndUpdateItems(SearchText);
    }

    private void UpdateHistory()
    {
        var showHistory = _settingsManager.ShowHistory;
        var history = showHistory != Resources.history_none ? _settingsManager.LoadHistory() : [];
        lock (_sync)
        {
            _historyItems = history;
        }
    }

    private void SettingsManagerOnHistoryChanged(object? sender, EventArgs e)
    {
        UpdateHistory();
        RequeryAndUpdateItems(SearchText);
    }

    private static IListItem[] Query(string query, List<ListItem> historySnapshot, ISettingsInterface settingsManager, IconInfo newSearchIcon)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filteredHistoryItems = settingsManager.ShowHistory != Resources.history_none
            ? ListHelpers.FilterList(historySnapshot, query).OfType<ListItem>()
            : [];

        var results = new List<ListItem>();

        if (!string.IsNullOrEmpty(query))
        {
            var searchTerm = query;
            var result = new ListItem(new SearchWebCommand(searchTerm, settingsManager))
            {
                Title = searchTerm,
                Subtitle = string.Format(CultureInfo.CurrentCulture, PluginOpen, BrowserInfo.Name ?? BrowserInfo.MSEdgeName),
                Icon = newSearchIcon,
            };
            results.Add(result);
        }

        results.AddRange(filteredHistoryItems);

        return [.. results];
    }

    private void RequeryAndUpdateItems(string search)
    {
        List<ListItem> historySnapshot;
        lock (_sync)
        {
            historySnapshot = _historyItems;
        }

        var items = Query(search ?? string.Empty, historySnapshot, _settingsManager, _newSearchIcon);

        lock (_sync)
        {
            _allItems = items;
        }

        RaiseItemsChanged();
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        RequeryAndUpdateItems(newSearch);
    }

    public override IListItem[] GetItems()
    {
        lock (_sync)
        {
            return _allItems;
        }
    }

    public void Dispose()
    {
        _settingsManager.HistoryChanged -= SettingsManagerOnHistoryChanged;
        GC.SuppressFinalize(this);
    }
}
