// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.Apps.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps;

public sealed partial class AllAppsPage : ListPage
{
    private readonly Lock _listLock = new();
    private readonly IAppCatalog _appCatalog;
    private readonly AllAppsSettings _settings;

    private AppListItem[] _allAppListItems = [];
    private bool _rebuildRequested;
    private Task? _rebuildTask;

    public AllAppsPage(IAppCatalog appCatalog, AllAppsSettings settings)
    {
        _appCatalog = appCatalog ?? throw new ArgumentNullException(nameof(appCatalog));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.Name = Resources.all_apps;
        this.Icon = Icons.AllAppsIcon;
        this.ShowDetails = true;
        this.IsLoading = true;
        this.PlaceholderText = Resources.search_installed_apps_placeholder;

        _appCatalog.Changed += OnCatalogChanged;
        _ = InitializePageAsync();
    }

    public override IListItem[] GetItems()
    {
        lock (_listLock)
        {
            return _allAppListItems;
        }
    }

    private void BuildListItems()
    {
        lock (_listLock)
        {
            var stopwatch = Stopwatch.StartNew();

            _allAppListItems = GetPrograms();

            stopwatch.Stop();
            Logger.LogTrace($"{nameof(AllAppsPage)}.{nameof(BuildListItems)} took: {stopwatch.ElapsedMilliseconds} ms");
        }
    }

    private async Task InitializePageAsync()
    {
        try
        {
            await _appCatalog.InitializeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to initialize the application index", ex);
        }

        QueueListRebuild();
    }

    private void OnCatalogChanged(object? sender, AppCatalogChangedEventArgs e)
    {
        QueueListRebuild();
    }

    private void QueueListRebuild()
    {
        lock (_listLock)
        {
            _rebuildRequested = true;
            this.IsLoading = true;

            if (_rebuildTask is null)
            {
                _rebuildTask = Task.Run(RebuildUntilCurrent);
            }
        }

        RaiseItemsChanged();
    }

    private void RebuildUntilCurrent()
    {
        while (true)
        {
            lock (_listLock)
            {
                _rebuildRequested = false;
            }

            try
            {
                BuildListItems();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to rebuild the application list", ex);
            }

            lock (_listLock)
            {
                if (_rebuildRequested)
                {
                    continue;
                }

                _rebuildTask = null;
                this.IsLoading = false;
            }

            RaiseItemsChanged();
            return;
        }
    }

    private AppListItem[] GetPrograms()
    {
        var items = new List<AppListItem>();
        var hideAppDescriptions = _settings.HideAppDescriptions;

        foreach (var app in _appCatalog.Items)
        {
            var item = new AppListItem(app, useThumbnails: true);
            item.Subtitle = hideAppDescriptions ? string.Empty : item.App.Subtitle;
            items.Add(item);
        }

        items.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.Ordinal));

        return [.. items];
    }
}
