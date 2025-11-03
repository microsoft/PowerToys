// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.Apps.Properties;
using Microsoft.CmdPal.Ext.Apps.State;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps;

public sealed partial class AllAppsPage : ListPage
{
    private readonly Lock _listLock = new();
    private readonly IAppCache _appCache;

    private AppItem[] allApps = [];
    private AppListItem[] unpinnedApps = [];
    private AppListItem[] pinnedApps = [];

    public AllAppsPage()
        : this(AppCache.Instance.Value)
    {
    }

    public AllAppsPage(IAppCache appCache)
    {
        _appCache = appCache ?? throw new ArgumentNullException(nameof(appCache));
        this.Name = Resources.all_apps;
        this.Icon = Icons.AllAppsIcon;
        this.ShowDetails = true;
        this.IsLoading = true;
        this.PlaceholderText = Resources.search_installed_apps_placeholder;

        // Subscribe to pin state changes to refresh the command provider
        PinnedAppsManager.Instance.PinStateChanged += OnPinStateChanged;

        Task.Run(() =>
        {
            lock (_listLock)
            {
                BuildListItems();
            }
        });
    }

    internal AppListItem[] GetPinnedApps()
    {
        BuildListItems();
        return pinnedApps;
    }

    public override IListItem[] GetItems()
    {
        // Build or update the list if needed
        BuildListItems();

        AppListItem[] allApps = [.. pinnedApps, .. unpinnedApps];
        return allApps;
    }

    private void BuildListItems()
    {
        if (allApps.Length == 0 || _appCache.ShouldReload())
        {
            lock (_listLock)
            {
                this.IsLoading = true;

                Stopwatch stopwatch = new();
                stopwatch.Start();

                var apps = GetPrograms();
                this.allApps = apps.AllApps;
                this.pinnedApps = apps.PinnedItems;
                this.unpinnedApps = apps.UnpinnedItems;

                this.IsLoading = false;

                _appCache.ResetReloadFlag();

                stopwatch.Stop();
                Logger.LogTrace($"{nameof(AllAppsPage)}.{nameof(BuildListItems)} took: {stopwatch.ElapsedMilliseconds} ms");
            }
        }
    }

    private AppItem[] GetAllApps()
    {
        List<AppItem> allApps = new();

        foreach (var uwpApp in _appCache.UWPs)
        {
            if (uwpApp.Enabled)
            {
                allApps.Add(uwpApp.ToAppItem());
            }
        }

        foreach (var win32App in _appCache.Win32s)
        {
            if (win32App.Enabled && win32App.Valid)
            {
                allApps.Add(win32App.ToAppItem());
            }
        }

        return [.. allApps];
    }

    internal (AppItem[] AllApps, AppListItem[] PinnedItems, AppListItem[] UnpinnedItems) GetPrograms()
    {
        var allApps = GetAllApps();
        var pinned = new List<AppListItem>();
        var unpinned = new List<AppListItem>();

        foreach (var app in allApps)
        {
            var isPinned = PinnedAppsManager.Instance.IsAppPinned(app.AppIdentifier);
            var appListItem = new AppListItem(app, true, isPinned);

            if (isPinned)
            {
                appListItem.Tags = [.. appListItem.Tags, new Tag() { Icon = Icons.PinIcon }];
                pinned.Add(appListItem);
            }
            else
            {
                unpinned.Add(appListItem);
            }
        }

        pinned.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.Ordinal));
        unpinned.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.Ordinal));

        return (
                allApps,
                pinned.ToArray(),
                unpinned.ToArray()
        );
    }

    private void OnPinStateChanged(object? sender, PinStateChangedEventArgs e)
    {
        /*
         * Rebuilding all the lists is pretty expensive.
         * So, instead, we'll just compare pinned items to move existing
         * items between the two lists.
        */
        AppItem? existingAppItem = null;

        foreach (var app in allApps)
        {
            if (app.AppIdentifier == e.AppIdentifier)
            {
                existingAppItem = app;
                break;
            }
        }

        if (existingAppItem is not null)
        {
            var appListItem = new AppListItem(existingAppItem, true, e.IsPinned);

            var newPinned = new List<AppListItem>(pinnedApps);
            var newUnpinned = new List<AppListItem>(unpinnedApps);

            if (e.IsPinned)
            {
                newPinned.Add(appListItem);

                foreach (var app in newUnpinned)
                {
                    if (app.AppIdentifier == e.AppIdentifier)
                    {
                        newUnpinned.Remove(app);
                        break;
                    }
                }
            }
            else
            {
                newUnpinned.Add(appListItem);

                foreach (var app in newPinned)
                {
                    if (app.AppIdentifier == e.AppIdentifier)
                    {
                        newPinned.Remove(app);
                        break;
                    }
                }
            }

            pinnedApps = newPinned.ToArray();
            unpinnedApps = newUnpinned.ToArray();
        }

        RaiseItemsChanged(0);
    }
}
