// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CmdPal.Ext.WindowsSettings.Classes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WindowsSettings;

internal sealed partial class WindowsSettingsListPage : DynamicListPage
{
    private readonly Classes.WindowsSettings _windowsSettings;

    public WindowsSettingsListPage(Classes.WindowsSettings windowsSettings)
    {
        Icon = IconHelpers.FromRelativePath("Assets\\WindowsSettings.svg");
        Name = "Windows Settings";
        Id = "com.microsoft.cmdpal.windowsSettings";
        _windowsSettings = windowsSettings;
    }

    public List<ListItem> Query(string query)
    {
        if (_windowsSettings?.Settings is null)
        {
            return new List<ListItem>(0);
        }

        var filteredList = _windowsSettings.Settings
            .Select(SearchScoringPredicate)
            .Where(scoredSetting => scoredSetting.Score > 0)
            .OrderByDescending(scoredSetting => scoredSetting.Score)
            .Select(scoredSetting => scoredSetting.Setting);

        var newList = ResultHelper.GetResultList(filteredList, query);
        return newList;

        // Rank settings by how they matched the search query. Order is:
        // 1. Exact Name (10 points)
        // 2. Name Starts With (8 points)
        // 3. Name (5 points)
        // 4. Area (4 points)
        // 5. AltName (2 points)
        // 6. Settings path (1 point)
        (WindowsSetting Setting, int Score) SearchScoringPredicate(WindowsSetting setting)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                // If no search string is entered skip query comparison.
                return (setting, 0);
            }

            if (string.Equals(setting.Name, query, StringComparison.OrdinalIgnoreCase))
            {
                return (setting, 10);
            }

            if (setting.Name.StartsWith(query, StringComparison.CurrentCultureIgnoreCase))
            {
                return (setting, 8);
            }

            if (setting.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            {
                return (setting, 5);
            }

            if (!(setting.Areas is null))
            {
                foreach (var area in setting.Areas)
                {
                    // Search for areas on normal queries.
                    if (area.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                    {
                        return (setting, 4);
                    }

                    // Search for Area only on queries with action char.
                    if (area.Contains(query.Replace(":", string.Empty), StringComparison.CurrentCultureIgnoreCase)
                    && query.EndsWith(":", StringComparison.CurrentCultureIgnoreCase))
                    {
                        return (setting, 4);
                    }
                }
            }

            if (!(setting.AltNames is null))
            {
                foreach (var altName in setting.AltNames)
                {
                    if (altName.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                    {
                        return (setting, 2);
                    }
                }
            }

            // Search by key char '>' for app name and settings path
            if (query.Contains('>') && ResultHelper.FilterBySettingsPath(setting, query))
            {
                return (setting, 1);
            }

            return (setting, 0);
        }
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        RaiseItemsChanged(0);
    }

    public override IListItem[] GetItems()
    {
        var items = Query(SearchText).ToArray();

        return items;
    }
}
