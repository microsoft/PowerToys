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
    private AppListItem[] allAppsSection = [];

    public AllAppsPage()
    {
        this.Name = Resources.all_apps;
        this.Icon = IconHelpers.FromRelativePath("Assets\\AllApps.svg");
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

    public override IListItem[] GetItems()
    {
        if (allAppsSection.Length == 0 || AppCache.Instance.Value.ShouldReload())
        {
            lock (_listLock)
            {
                BuildListItems();
            }
        }

        return allAppsSection;
    }

    private void BuildListItems()
    {
        this.IsLoading = true;

        Stopwatch stopwatch = new();
        stopwatch.Start();

        var apps = GetPrograms();

        this.allAppsSection = apps
                        .Select((app) => new AppListItem(app, true))
                        .ToArray();

        this.IsLoading = false;

        AppCache.Instance.Value.ResetReloadFlag();

        stopwatch.Stop();
        Logger.LogTrace($"{nameof(AllAppsPage)}.{nameof(BuildListItems)} took: {stopwatch.ElapsedMilliseconds} ms");
    }

    internal List<AppItem> GetPrograms()
    {
        var uwpResults = AppCache.Instance.Value.UWPs
            .Where((application) => application.Enabled)
            .Select(app => app.ToAppItem());

        var win32Results = AppCache.Instance.Value.Win32s
            .Where((application) => application.Enabled && application.Valid)
            .Select(app => app.ToAppItem());

        var allApps = uwpResults.Concat(win32Results).ToList();

        // Sort with pinned apps first, then alphabetically
        return allApps.OrderBy(app => !PinnedAppsManager.Instance.IsAppPinned(app.AppIdentifier))
                      .ThenBy(app => app.Name)
                      .ToList();
    }

    private void OnPinStateChanged(object? sender, System.EventArgs e)
    {
        // Emptying this list so the BuildList attempts to recreate the list of
        // AppItems with updated order for pinned apps & updated context menus
        allAppsSection = [];
        RaiseItemsChanged(0);
    }
}
