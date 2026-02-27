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
    private volatile bool _isRebuilding;

    public AllAppsPage()
        : this(AppCache.Instance.Value, AllAppsSettings.Instance.Settings)
    {
    }

    public AllAppsPage(IAppCache appCache)
        : this(appCache, new Settings())
    {
    }

    public AllAppsPage(IAppCache appCache, Settings settings)
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

        settings.SettingsChanged += (s, a) =>
        {
            if (_appCache.IsIndexing && !_isRebuilding)
            {
                // A background re-index is in progress. Show a loading
                // indicator and banner, then wait for it to finish.
                _isRebuilding = true;
                this.IsLoading = true;
                RaiseItemsChanged();

                _ = Task.Run(async () =>
                {
                    while (_appCache.IsIndexing)
                    {
                        await Task.Delay(200).ConfigureAwait(false);
                    }

                    lock (_listLock)
                    {
                        allAppListItems = GetPrograms();
                        _appCache.ResetReloadFlag();
                    }

                    _isRebuilding = false;
                    this.IsLoading = false;
                    RaiseItemsChanged();
                });
            }
            else
            {
                RaiseItemsChanged();
            }
        };
    }

    public override IListItem[] GetItems()
    {
        if (!_isRebuilding)
        {
            // Build or update the list if needed
            BuildListItems();
        }

        if (_isRebuilding && allAppListItems.Length > 0)
        {
            var banner = new ListItem(new NoOpCommand())
            {
                Title = Resources.refreshing_app_list,
                Icon = Icons.Reloading,
            };

            var result = new IListItem[allAppListItems.Length + 3];
            result[0] = new Separator(Resources.section_status);
            result[1] = banner;
            result[2] = new Separator(Resources.section_all_apps);
            allAppListItems.CopyTo(result, 3);
            return result;
        }

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
                items.Add(new AppListItem(uwpApp.ToAppItem(), useThumbnails: true));
            }
        }

        foreach (var win32App in _appCache.Win32s)
        {
            if (win32App.Enabled && win32App.Valid)
            {
                items.Add(new AppListItem(win32App.ToAppItem(), useThumbnails: true));
            }
        }

        items.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.Ordinal));

        return [.. items];
    }
}
