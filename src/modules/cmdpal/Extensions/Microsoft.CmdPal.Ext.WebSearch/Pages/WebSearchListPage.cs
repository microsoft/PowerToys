// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using Microsoft.CmdPal.Ext.WebSearch.Commands;
using Microsoft.CmdPal.Ext.WebSearch.Helpers;
using Microsoft.CmdPal.Ext.WebSearch.Helpers.Browser;
using Microsoft.CmdPal.Ext.WebSearch.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WebSearch.Pages;

internal sealed partial class WebSearchListPage : DynamicListPage, IDisposable
{
    private readonly ISettingsInterface _settingsManager;
    private readonly IBrowserInfoService _browserInfoService;
    private readonly Lock _sync = new();
    private static readonly CompositeFormat PluginInBrowserName = System.Text.CompositeFormat.Parse(Properties.Resources.plugin_in_browser_name);
    private static readonly CompositeFormat PluginOpen = System.Text.CompositeFormat.Parse(Properties.Resources.plugin_open);
    private IListItem[] _allItems = [];
    private List<ListItem> _historyItems = [];

    public WebSearchListPage(ISettingsInterface settingsManager, IBrowserInfoService browserInfoService)
    {
        ArgumentNullException.ThrowIfNull(settingsManager);

        Name = Resources.command_item_title;
        Title = Resources.command_item_title;
        Icon = Icons.WebSearch;
        Id = "com.microsoft.cmdpal.websearch";

        _settingsManager = settingsManager;
        _browserInfoService = browserInfoService;
        _settingsManager.HistoryChanged += SettingsManagerOnHistoryChanged;

        // It just looks viewer to have string twice on the page, and default placeholder is good enough
        PlaceholderText = _allItems.Length > 0 ? Resources.plugin_description : string.Empty;

        EmptyContent = new CommandItem(new NoOpCommand())
        {
            Icon = Icon,
            Title = Resources.plugin_description,
            Subtitle = string.Format(CultureInfo.CurrentCulture, PluginInBrowserName, browserInfoService.GetDefaultBrowser()?.Name ?? Resources.default_browser),
        };

        UpdateHistory();
        RequeryAndUpdateItems(SearchText);
    }

    private void SettingsManagerOnHistoryChanged(object? sender, EventArgs e)
    {
        UpdateHistory();
        RequeryAndUpdateItems(SearchText);
    }

    private void UpdateHistory()
    {
        List<ListItem> history = [];

        if (_settingsManager.HistoryItemCount > 0)
        {
            var items = _settingsManager.HistoryItems;
            for (var index = items.Count - 1; index >= 0; index--)
            {
                var historyItem = items[index];
                history.Add(new ListItem(new SearchWebCommand(historyItem.SearchString, _settingsManager, _browserInfoService))
                {
                    Icon = Icons.History,
                    Title = historyItem.SearchString,
                    Subtitle = historyItem.Timestamp.ToString("g", CultureInfo.InvariantCulture),
                });
            }
        }

        lock (_sync)
        {
            _historyItems = history;
        }
    }

    private static IListItem[] Query(string query, List<ListItem> historySnapshot, ISettingsInterface settingsManager, IBrowserInfoService browserInfoService)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filteredHistoryItems = settingsManager.HistoryItemCount > 0
            ? ListHelpers.FilterList(historySnapshot, query)
            : [];

        var results = new List<IListItem>();

        if (!string.IsNullOrEmpty(query))
        {
            var searchTerm = query;
            var result = new ListItem(new SearchWebCommand(searchTerm, settingsManager, browserInfoService))
            {
                Title = searchTerm,
                Subtitle = string.Format(CultureInfo.CurrentCulture, PluginOpen, browserInfoService.GetDefaultBrowser()?.Name ?? Resources.default_browser),
                Icon = Icons.Search,
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

        var items = Query(search ?? string.Empty, historySnapshot, _settingsManager, _browserInfoService);

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
