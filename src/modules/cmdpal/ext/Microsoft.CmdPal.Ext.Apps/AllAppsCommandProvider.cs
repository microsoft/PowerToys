// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CmdPal.Ext.Apps.Helpers;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.Apps.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps;

public partial class AllAppsCommandProvider : CommandProvider
{
    public const string WellKnownId = "AllApps";

    private readonly AllAppsPage _page;
    private readonly AllAppsSettings _settings;
    private readonly CommandItem _listItem;

    public AllAppsCommandProvider(AllAppsPage page, AllAppsSettings settings)
    {
        _page = page ?? throw new ArgumentNullException(nameof(page));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Id = WellKnownId;
        DisplayName = Resources.installed_apps;
        Icon = Icons.AllAppsIcon;
        Settings = _settings.Settings;

        _listItem = new(_page)
        {
            MoreCommands = [new CommandContextItem(_settings.Settings.SettingsPage)],
        };
    }

    public int TopLevelResultLimit => _settings.EffectiveSearchResultLimit;

    public override ICommandItem[] TopLevelCommands() => [_listItem];

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

    public override ICommandItem? GetCommandItem(string id)
    {
        var items = _page.GetItems();
        foreach (var item in items)
        {
            if (item.Command.Id == id)
            {
                return item;
            }
        }

        var alias = items.OfType<AppListItem>().FirstOrDefault(item => item.App.CommandIds.Contains(id, StringComparer.Ordinal));
        return alias is null ? null : new AppCommandAlias(alias, id);
    }
}
