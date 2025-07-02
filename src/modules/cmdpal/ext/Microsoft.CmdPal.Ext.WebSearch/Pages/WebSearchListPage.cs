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
    private List<ListItem> allItems;

    public WebSearchListPage(SettingsManager settingsManager)
    {
        Name = Resources.command_item_title;
        Title = Resources.command_item_title;
        PlaceholderText = Resources.plugin_description;
        Icon = IconHelpers.FromRelativePath("Assets\\WebSearch.png");
        allItems = [new(new NoOpCommand())
        {
            Icon = IconHelpers.FromRelativePath("Assets\\WebSearch.png"),
            Title = Properties.Resources.plugin_description,
            Subtitle = string.Format(CultureInfo.CurrentCulture, PluginOpen, BrowserInfo.Name ?? BrowserInfo.MSEdgeName),
        }
        ];
        Id = "com.microsoft.cmdpal.websearch";
        _settingsManager = settingsManager;
        _historyItems = _settingsManager.ShowHistory != Resources.history_none ? _settingsManager.LoadHistory() : null;
        if (_historyItems != null)
        {
            allItems.AddRange(_historyItems);
        }
    }

    public List<ListItem> Query(string query)
    {
        ArgumentNullException.ThrowIfNull(query);
        IEnumerable<ListItem>? filteredHistoryItems = null;

        if (_historyItems != null)
        {
            filteredHistoryItems = _settingsManager.ShowHistory != Resources.history_none ? ListHelpers.FilterList(_historyItems, query).OfType<ListItem>() : null;
        }

        var results = new List<ListItem>();

        // empty query
        if (string.IsNullOrEmpty(query))
        {
            results.Add(new ListItem(new SearchWebCommand(string.Empty, _settingsManager))
            {
                Title = Properties.Resources.plugin_description,
                Subtitle = string.Format(CultureInfo.CurrentCulture, PluginInBrowserName, BrowserInfo.Name ?? BrowserInfo.MSEdgeName),
                Icon = new IconInfo(_iconPath),
            });
        }
        else
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

        if (filteredHistoryItems != null)
        {
            results.AddRange(filteredHistoryItems);
        }

        return results;
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        allItems = [.. Query(newSearch)];
        RaiseItemsChanged(0);
    }

    public override IListItem[] GetItems() => [.. allItems];
}
