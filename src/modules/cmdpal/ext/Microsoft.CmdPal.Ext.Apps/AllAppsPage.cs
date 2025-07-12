// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Apps.Commands;
using Microsoft.CmdPal.Ext.Apps.Helpers;
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
                this.pinnedApps = apps.PinnedItems;
                this.unpinnedApps = apps.UnpinnedItems;

                this.IsLoading = false;

                AppCache.Instance.Value.ResetReloadFlag();

                stopwatch.Stop();
                Logger.LogTrace($"{nameof(AllAppsPage)}.{nameof(BuildListItems)} took: {stopwatch.ElapsedMilliseconds} ms");
            }
        }
    }

    internal (AppItem[] AllApps, AppListItem[] PinnedItems, AppListItem[] UnpinnedItems) GetPrograms()
    {
        var uwpResults = AppCache.Instance.Value.UWPs
            .Where((application) => application.Enabled)
            .Select(app => app.ToAppItem());

        var win32Results = AppCache.Instance.Value.Win32s
            .Where((application) => application.Enabled && application.Valid)
            .Select(app => app.ToAppItem());

        var allApps = uwpResults.Concat(win32Results).ToList();

        var pinned = new List<AppListItem>();
        var unpinned = new List<AppListItem>();

        foreach (var app in allApps)
        {
            var isPinned = PinnedAppsManager.Instance.IsAppPinned(app.AppIdentifier);
            app.Commands?.AddRange(AddPinCommands(app, isPinned));

            var appListItem = new AppListItem(app, true);

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

    private void OnPinStateChanged(object? sender, System.EventArgs e)
    {
        /*
         * Rebuilding all the lists is pretty expensive.
         * So, instead, we'll just compare pinned items to move existing
         * items between the two lists.
        */

        var pinnedIds = PinnedAppsManager.Instance.GetPinnedAppIdentifiers();

        var pinnedApps = allApps
                            .Where(w => pinnedIds.Contains(w.AppIdentifier))
                            .Select(app =>
                            {
                                app.Commands?.AddRange(AddPinCommands(app, true));
                                return new AppListItem(app, true);
                            })
                            .OrderBy(o => o.Title)
                            .ToArray();

        var unpinnedApps = allApps
                            .Where(app => !pinnedIds.Contains(app.AppIdentifier))
                            .Select(app =>
                            {
                                app.Commands?.AddRange(AddPinCommands(app, false));
                                return new AppListItem(app, true);
                            })
                            .OrderBy(o => o.Title)
                            .ToArray();

        this.pinnedApps = pinnedApps;
        this.unpinnedApps = unpinnedApps;

        RaiseItemsChanged(0);
    }

    private List<IContextItem> AddPinCommands(AppItem app, bool isPinned)
    {
        var commands = new List<IContextItem>();

        commands.Add(new SeparatorContextItem());

        // 0x50 = P
        // Full key chord would be Ctrl+P
        var pinKeyChord = KeyChordHelpers.FromModifiers(true, false, false, false, 0x50, 0);

        if (isPinned)
        {
            commands.Add(
                new CommandContextItem(
                    new UnpinAppCommand(app.AppIdentifier))
                {
                    RequestedShortcut = pinKeyChord,
                });
        }
        else
        {
            commands.Add(
                new CommandContextItem(
                    new PinAppCommand(app.AppIdentifier))
                {
                    RequestedShortcut = pinKeyChord,
                });
        }

        return commands;
    }
}
