// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CmdPal.Ext.WindowsSettings.Classes;
using Microsoft.CmdPal.Ext.WindowsSettings.Helpers;
using Microsoft.CmdPal.Ext.WindowsSettings.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WindowsSettings;

internal sealed partial class WindowsSettingsListPage : DynamicListPage
{
    private readonly Classes.WindowsSettings _windowsSettings;

    public WindowsSettingsListPage(Classes.WindowsSettings windowsSettings)
    {
        Icon = Icons.WindowsSettingsIcon;
        Name = Resources.settings_title;
        Id = "com.microsoft.cmdpal.windowsSettings";
        _windowsSettings = windowsSettings;

        EmptyContent = new CommandItem(new NoOpCommand())
        {
            Icon = Icon,
            Title = Resources.settings_subtitle,
            Subtitle = Resources.PluginNoResultsMessage + "\n\n" + Resources.PluginNoResultsMessageHelp,
        };
    }

    public WindowsSettingsListPage(Classes.WindowsSettings windowsSettings, string query)
        : this(windowsSettings)
    {
        SearchText = query;
    }

    public List<ListItem> Query(string query)
    {
        if (_windowsSettings?.Settings is null)
        {
            return new List<ListItem>(0);
        }

        var filteredList = _windowsSettings.Settings;
        if (!string.IsNullOrEmpty(query))
        {
            filteredList = filteredList
                .Select(setting => ScoringHelper.SearchScoringPredicate(query, setting))
                .Where(scoredSetting => scoredSetting.Score > 0)
                .OrderByDescending(scoredSetting => scoredSetting.Score)
                .Select(scoredSetting => scoredSetting.Setting);
        }
        else
        {
            filteredList = filteredList
                .Where(s => s.AppHomepageScore > 0)
                .OrderByDescending(s => s.AppHomepageScore);
        }

        var newList = ResultHelper.GetResultList(filteredList);
        return newList;
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        if (oldSearch != newSearch)
        {
            RaiseItemsChanged(0);
        }
    }

    public override IListItem[] GetItems()
    {
        var items = Query(SearchText).ToArray();

        return items;
    }
}
