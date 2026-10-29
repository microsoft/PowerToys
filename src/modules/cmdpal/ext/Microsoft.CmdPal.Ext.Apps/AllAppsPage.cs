// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.Apps.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps;

public sealed partial class AllAppsPage : ListPage
{
    private readonly Lock _listLock = new();
    private readonly IAppCache _appCache;
    private readonly AllAppsSettings _settings;

    private volatile AppListSnapshot _snapshot = AppListSnapshot.Empty;

    public AllAppsPage()
        : this(AppCache.Instance.Value)
    {
    }

    public AllAppsPage(IAppCache appCache)
        : this(appCache, AllAppsSettings.Instance)
    {
    }

    internal AllAppsPage(IAppCache appCache, AllAppsSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _appCache = appCache ?? throw new ArgumentNullException(nameof(appCache));
        this.Name = Resources.all_apps;
        this.Icon = Icons.AllAppsIcon;
        this.ShowDetails = true;
        this.IsLoading = true;
        this.PlaceholderText = Resources.search_installed_apps_placeholder;

        Task.Run(() =>
        {
            lock (_listLock)
            {
                BuildListItems();
            }
        });
    }

    public override IListItem[] GetItems()
    {
        // Build or update the list if needed
        BuildListItems();

        return _snapshot.Items;
    }

    /// <summary>
    /// Looks up an item in the most recently published app catalog without waiting for a reload.
    /// </summary>
    public bool TryGetCurrentItem(string commandId, out AppListItem? item)
    {
        if (string.IsNullOrEmpty(commandId))
        {
            item = null;
            return false;
        }

        item = _snapshot.Commands.GetApp(commandId);
        return item is not null;
    }

    private void BuildListItems()
    {
        if (_snapshot.Items.Length == 0 || _appCache.ShouldReload())
        {
            lock (_listLock)
            {
                this.IsLoading = true;

                Stopwatch stopwatch = new();
                stopwatch.Start();

                var items = GetPrograms();
                var aliases = _settings.RetainAppCommandAliases(items.Select(item => item.App));
                var commands = new AppListItemSnapshot(items, [], commandAliases: aliases);
                _snapshot = new AppListSnapshot(items, commands);

                this.IsLoading = false;

                _appCache.ResetReloadFlag();

                stopwatch.Stop();
                Logger.LogTrace($"{nameof(AllAppsPage)}.{nameof(BuildListItems)} took: {stopwatch.ElapsedMilliseconds} ms");
            }
        }
    }

    private AppListItem[] GetPrograms()
    {
        var items = new List<AppListItem>();

        foreach (var uwpApp in _appCache.UWPs)
        {
            if (uwpApp.Enabled)
            {
                items.Add(new AppListItem(uwpApp.ToAppItem(), true));
            }
        }

        foreach (var win32App in _appCache.Win32s)
        {
            if (win32App.Enabled && win32App.Valid)
            {
                items.Add(new AppListItem(win32App.ToAppItem(), true));
            }
        }

        items.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.Ordinal));

        if (_settings.HideAppDescriptions)
        {
            foreach (var item in items)
            {
                item.Subtitle = string.Empty;
            }
        }

        return items.GroupBy(item => item.Command!.Id, StringComparer.OrdinalIgnoreCase).Select(group =>
        {
            var preferred = group.First();
            preferred.App.CommandIds = group.SelectMany(item => item.App.CommandIds).Distinct(StringComparer.Ordinal).ToArray();
            return preferred;
        }).ToArray();
    }

    private sealed record AppListSnapshot(
        AppListItem[] Items,
        AppListItemSnapshot Commands)
    {
        public static AppListSnapshot Empty { get; } = new([], new AppListItemSnapshot([], []));
    }

    /// <summary>Captures the published command lookup without waiting for discovery.</summary>
    public AppListItemSnapshot GetSnapshot() => _snapshot.Commands;
}
