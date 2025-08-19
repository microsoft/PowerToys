// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CmdPal.Ext.WebSearch.Commands;
using Microsoft.CmdPal.Ext.WebSearch.Helpers;
using Microsoft.CmdPal.Ext.WebSearch.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using BrowserInfo = Microsoft.CmdPal.Ext.WebSearch.Helpers.DefaultBrowserInfo;

namespace Microsoft.CmdPal.Ext.WebSearch.Pages;

internal sealed partial class WebSearchListPage : DynamicListPage
{
    private readonly string _iconPath = string.Empty;
    private readonly List<ListItem>? _historyItems;
    private readonly SettingsManager _settingsManager;
    private static readonly CompositeFormat PluginInBrowserName = System.Text.CompositeFormat.Parse(Properties.Resources.plugin_in_browser_name);
    private static readonly CompositeFormat PluginOpen = System.Text.CompositeFormat.Parse(Properties.Resources.plugin_open);
    private List<ListItem> _allItems;

    public WebSearchListPage(SettingsManager settingsManager)
    {
        Name = Resources.command_item_title;
        Title = Resources.command_item_title;
        Icon = IconHelpers.FromRelativePath("Assets\\WebSearch.png");
        _allItems = [];
        Id = "com.microsoft.cmdpal.websearch";
        _settingsManager = settingsManager;
        _historyItems = _settingsManager.ShowHistory != Resources.history_none ? _settingsManager.LoadHistory() : null;
        if (_historyItems is not null)
        {
            _allItems.AddRange(_historyItems);
        }

        // It just looks viewer to have string twice on the page, and default placeholder is good enough
        PlaceholderText = _allItems.Count > 0 ? Resources.plugin_description : string.Empty;

        EmptyContent = new CommandItem(new NoOpCommand())
        {
            Icon = Icon,
            Title = Properties.Resources.plugin_description,
            Subtitle = string.Format(CultureInfo.CurrentCulture, PluginInBrowserName, BrowserInfo.Name ?? BrowserInfo.MSEdgeName),
        };
    }

    public List<ListItem> Query(string query)
    {
        ArgumentNullException.ThrowIfNull(query);
        IEnumerable<ListItem>? filteredHistoryItems = null;

        if (_historyItems is not null)
        {
            filteredHistoryItems = _settingsManager.ShowHistory != Resources.history_none ? ListHelpers.FilterList(_historyItems, query).OfType<ListItem>() : null;
        }

        var results = new List<ListItem>();

        if (!string.IsNullOrEmpty(query))
        {
            var searchTerm = query;
            var result = new ListItem(new SearchWebCommand(searchTerm, _settingsManager))
            {
                Title = searchTerm,
                Subtitle = string.Format(CultureInfo.CurrentCulture, PluginOpen, BrowserInfo.Name ?? BrowserInfo.MSEdgeName),
                Icon = new IconInfo(_iconPath),
            };
            results.Add(result);
        }

        if (filteredHistoryItems is not null)
        {
            results.AddRange(filteredHistoryItems);
        }

        return results;
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _allItems = [.. Query(newSearch)];
        RaiseItemsChanged(0);
    }

    public override IListItem[] GetItems() => [.. _allItems];
}
