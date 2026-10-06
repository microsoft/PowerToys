// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
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
    private readonly IAppListItemSource _appListItemSource;
    private readonly AllAppsSettings _settings;
    private readonly CommandItem _listItem;
    private AppListItemSnapshot _snapshot;

    public int TopLevelResultLimit => _appListItemSource.TopLevelResultLimit;

    /// <summary>Initializes a new instance of the <see cref="AllAppsCommandProvider"/> class. Creates the Apps provider over the shared application page, snapshot source, and preferences.</summary>
    public AllAppsCommandProvider(
        AllAppsPage page,
        IAppListItemSource appListItemSource,
        AllAppsSettings settings)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(appListItemSource);
        ArgumentNullException.ThrowIfNull(settings);

        _page = page;
        _appListItemSource = appListItemSource;
        _settings = settings;
        Id = WellKnownId;
        DisplayName = Resources.installed_apps;
        Icon = Icons.AllAppsIcon;
        Settings = _settings.Settings;

        _listItem = new(_page)
        {
            MoreCommands =
            [
                .. _page.MoreCommands,
                new CommandContextItem(_settings.Settings.SettingsPage),
            ],
        };
        _snapshot = _appListItemSource.GetSnapshot();
        _appListItemSource.Changed += OnAppListChanged;
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return [_listItem];
    }

    private void OnAppListChanged(object? sender, EventArgs args)
    {
        var snapshot = _appListItemSource.GetSnapshot();
        var previous = Interlocked.Exchange(ref _snapshot, snapshot);
        if (!snapshot.HasSameCommandResolution(previous))
        {
            RaiseItemsChanged();
        }
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        _appListItemSource.Changed -= OnAppListChanged;
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    public ICommandItem? LookupAppByPackageFamilyName(string packageFamilyName, bool requireSingleMatch)
    {
        if (string.IsNullOrEmpty(packageFamilyName))
        {
            return null;
        }

        var items = _appListItemSource.GetSnapshot().VisibleItems;
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

        var items = _appListItemSource.GetSnapshot().VisibleItems;
        List<ICommandItem> matches = [];

        foreach (var item in items)
        {
            if (item is not AppListItem appListItem || string.IsNullOrEmpty(appListItem.App.ResolvedTarget))
            {
                continue;
            }

            foreach (var candidate in candidates)
            {
                if (string.Equals(appListItem.App.ResolvedTarget, candidate, StringComparison.OrdinalIgnoreCase))
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
        return _appListItemSource.GetSnapshot().GetCommandItem(id);
    }
}
