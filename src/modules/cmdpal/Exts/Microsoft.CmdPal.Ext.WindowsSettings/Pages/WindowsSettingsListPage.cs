// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.CmdPal.Ext.WindowsSettings.Classes;
using Microsoft.CmdPal.Ext.WindowsSettings.Helpers;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using Microsoft.UI.Windowing;

namespace Microsoft.CmdPal.Ext.WindowsSettings;

internal sealed partial class WindowsSettingsListPage : DynamicListPage
{
    private readonly string _defaultIconPath;
    private readonly Classes.WindowsSettings _windowsSettings;

    public WindowsSettingsListPage(Classes.WindowsSettings windowsSettings)
    {
        Icon = new(string.Empty);
        Name = "Windows Settings";
        _defaultIconPath = "Images/WindowsSettings.light.png";
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

        var newList = ResultHelper.GetResultList(filteredList, query, _defaultIconPath);
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
            if (query.Contains('>'))
            {
                return ResultHelper.FilterBySettingsPath(found, query);
            }

            return false;
        }
    }

    public override ISection[] GetItems(string query)
    {
        ListItem[] items = Query(query).ToArray();

        return new ISection[] { new ListSection() { Title = "Windows Settings", Items = items } };
    }
}
