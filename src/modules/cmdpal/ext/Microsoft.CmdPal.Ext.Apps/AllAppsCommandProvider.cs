// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CmdPal.Ext.Apps.Helpers;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.Apps.Properties;
using Microsoft.CmdPal.Ext.Apps.State;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps;

public partial class AllAppsCommandProvider : CommandProvider
{
    public const string WellKnownId = "AllApps";

    public static readonly AllAppsPage Page = new();

    private readonly AllAppsPage _page;
    private readonly CommandItem _listItem;

    public AllAppsCommandProvider()
        : this(Page)
    {
    }

    public AllAppsCommandProvider(AllAppsPage page)
    {
        _page = page ?? throw new ArgumentNullException(nameof(page));
        Id = WellKnownId;
        DisplayName = Resources.installed_apps;
        Icon = Icons.AllAppsIcon;
        Settings = AllAppsSettings.Instance.Settings;

        _listItem = new(_page)
        {
            MoreCommands = [new CommandContextItem(AllAppsSettings.Instance.Settings.SettingsPage)],
        };

        // Subscribe to pin state changes to refresh the command provider
        PinnedAppsManager.Instance.PinStateChanged += OnPinStateChanged;
    }

    public static int TopLevelResultLimit
    {
        get
        {
            var limitSetting = AllAppsSettings.Instance.SearchResultLimit;

            if (limitSetting is null)
            {
                return 10;
            }

            var quantity = 10;

            if (int.TryParse(limitSetting, out var result))
            {
                quantity = result < 0 ? quantity : result;
            }

            return quantity;
        }
    }

    public override ICommandItem[] TopLevelCommands() => [_listItem, .. _page.GetPinnedApps()];

    public ICommandItem? LookupAppByPackageFamilyName(string packageFamilyName, bool requireSingleMatch)
    {
        if (string.IsNullOrEmpty(packageFamilyName))
        {
            return null;
        }

        var items = _page.GetItems();
        List<ICommandItem> matches = [];

        foreach (var item in items)
        {
            if (item is AppListItem appItem && string.Equals(packageFamilyName, appItem.App.PackageFamilyName, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(item);
                if (!requireSingleMatch)
                {
                    // Return early if we don't require uniqueness.
                    return item;
                }
            }
        }

        return requireSingleMatch && matches.Count == 1 ? matches[0] : null;
    }

    public ICommandItem? LookupAppByProductCode(string productCode, bool requireSingleMatch)
    {
        if (string.IsNullOrEmpty(productCode))
        {
            return null;
        }

        if (!UninstallRegistryAppLocator.TryGetInstallInfo(productCode, out _, out var candidates) || candidates.Count <= 0)
        {
            return null;
        }

        var items = _page.GetItems();
        List<ICommandItem> matches = [];

        foreach (var item in items)
        {
            if (item is not AppListItem appListItem || string.IsNullOrEmpty(appListItem.App.FullExecutablePath))
            {
                continue;
            }

            foreach (var candidate in candidates)
            {
                if (string.Equals(appListItem.App.FullExecutablePath, candidate, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(item);
                    if (!requireSingleMatch)
                    {
                        return item;
                    }
                }
            }
        }

        return requireSingleMatch && matches.Count == 1 ? matches[0] : null;
    }

    public ICommandItem? LookupAppByDisplayName(string displayName)
    {
        var items = _page.GetItems();

        var nameMatches = new List<ICommandItem>();
        ICommandItem? bestAppMatch = null;
        var bestLength = -1;

        foreach (var item in items)
        {
            if (item.Title is null)
            {
                continue;
            }

            // We're going to do this search in two directions:
            // First, is this name a substring of any app...
            if (item.Title.Contains(displayName))
            {
                nameMatches.Add(item);
            }

            // ... Then, does any app have this name as a substring ...
            // Only get one of these - "Terminal Preview" contains both "Terminal" and "Terminal Preview", so just take the best one
            if (displayName.Contains(item.Title))
            {
                if (item.Title.Length > bestLength)
                {
                    bestLength = item.Title.Length;
                    bestAppMatch = item;
                }
            }
        }

        // ... Now, combine those two
        List<ICommandItem> both = bestAppMatch is null ? nameMatches : [.. nameMatches, bestAppMatch];

        if (both.Count == 1)
        {
            return both[0];
        }
        else if (nameMatches.Count == 1 && bestAppMatch is not null)
        {
            if (nameMatches[0] == bestAppMatch)
            {
                return nameMatches[0];
            }
        }

        return null;
    }

    private void OnPinStateChanged(object? sender, System.EventArgs e)
    {
        RaiseItemsChanged(0);
    }
}
