// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

    private AppItem[] allApps = [];
    private AppListItem[] unpinnedApps = [];
    private AppListItem[] pinnedApps = [];

    public AllAppsPage()
    {
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
        return pinnedApps.Concat(unpinnedApps).ToArray();
    }

    private void BuildListItems()
    {
        if (allApps.Length == 0 || AppCache.Instance.Value.ShouldReload())
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

                AppCache.Instance.Value.ResetReloadFlag();

                stopwatch.Stop();
                Logger.LogTrace($"{nameof(AllAppsPage)}.{nameof(BuildListItems)} took: {stopwatch.ElapsedMilliseconds} ms");
            }
        }
    }

    private AppItem[] GetAllApps()
    {
        var uwpResults = AppCache.Instance.Value.UWPs
           .Where((application) => application.Enabled)
           .Select(app => app.ToAppItem());

        var win32Results = AppCache.Instance.Value.Win32s
            .Where((application) => application.Enabled && application.Valid)
            .Select(app => app.ToAppItem());

        var allApps = uwpResults.Concat(win32Results).ToArray();
        return allApps;
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
                appListItem.Tags = appListItem.Tags
                                            .Concat([new Tag() { Icon = Icons.PinIcon }])
                                            .ToArray();
                pinned.Add(appListItem);
            }
            else
            {
                unpinned.Add(appListItem);
            }
        }

        return (
                allApps
                    .ToArray(),
                pinned
                    .OrderBy(app => app.Title)
                    .ToArray(),
                unpinned
                    .OrderBy(app => app.Title)
                    .ToArray());
    }

    private void OnPinStateChanged(object? sender, PinStateChangedEventArgs e)
    {
        /*
         * Rebuilding all the lists is pretty expensive.
         * So, instead, we'll just compare pinned items to move existing
         * items between the two lists.
        */
        var existingAppItem = allApps.FirstOrDefault(f => f.AppIdentifier == e.AppIdentifier);

        if (existingAppItem is not null)
        {
            var appListItem = new AppListItem(existingAppItem, true, e.IsPinned);

            if (e.IsPinned)
            {
                // Remove it from the unpinned apps array
                this.unpinnedApps = this.unpinnedApps
                                            .Where(app => app.AppIdentifier != existingAppItem.AppIdentifier)
                                            .OrderBy(app => app.Title)
                                            .ToArray();

                var newPinned = this.pinnedApps.ToList();
                newPinned.Add(appListItem);

                this.pinnedApps = newPinned
                                        .OrderBy(app => app.Title)
                                        .ToArray();
            }
            else
            {
                // Remove it from the pinned apps array
                this.pinnedApps = this.pinnedApps
                                            .Where(app => app.AppIdentifier != existingAppItem.AppIdentifier)
                                            .OrderBy(app => app.Title)
                                            .ToArray();

                var newUnpinned = this.unpinnedApps.ToList();
                newUnpinned.Add(appListItem);

                this.unpinnedApps = newUnpinned
                                        .OrderBy(app => app.Title)
                                        .ToArray();
            }

            RaiseItemsChanged(0);
        }
    }
}
