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
            .Where(Predicate)
            .OrderBy(found => found.Name);

        var newList = ResultHelper.GetResultList(filteredList, query);
        return newList;

        bool Predicate(WindowsSetting found)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                // If no search string is entered skip query comparison.
                return true;
            }

            if (found.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            {
                return true;
            }

            if (!(found.Areas is null))
            {
                foreach (var area in found.Areas)
                {
                    // Search for areas on normal queries.
                    if (area.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                    {
                        return true;
                    }

                    // Search for Area only on queries with action char.
                    if (area.Contains(query.Replace(":", string.Empty), StringComparison.CurrentCultureIgnoreCase)
                    && query.EndsWith(":", StringComparison.CurrentCultureIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            if (!(found.AltNames is null))
            {
                foreach (var altName in found.AltNames)
                {
                    if (altName.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            // Search by key char '>' for app name and settings path
            return query.Contains('>') ? ResultHelper.FilterBySettingsPath(found, query) : false;
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
