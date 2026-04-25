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
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps;

public sealed partial class AllAppsPage : ListPage
{
    private readonly Lock _listLock = new();
    private readonly IAppCache _appCache;

    private AppListItem[] allAppListItems = [];

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

        return allAppListItems;
    }

    private void BuildListItems()
    {
        if (allAppListItems.Length == 0 || _appCache.ShouldReload())
        {
            lock (_listLock)
            {
                this.IsLoading = true;

                Stopwatch stopwatch = new();
                stopwatch.Start();

                this.allAppListItems = GetPrograms();

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

        return [.. items];
    }
}
