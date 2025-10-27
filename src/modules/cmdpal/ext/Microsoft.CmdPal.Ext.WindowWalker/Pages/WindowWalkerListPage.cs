// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CmdPal.Ext.WindowWalker.Components;
using Microsoft.CmdPal.Ext.WindowWalker.Helpers;
using Microsoft.CmdPal.Ext.WindowWalker.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.UI.Dispatching;

namespace Microsoft.CmdPal.Ext.WindowWalker.Pages;

internal sealed partial class WindowWalkerListPage : DynamicListPage, IDisposable
{
    private readonly List<WindowWalkerListItem> _results = new();
    private readonly SettingsManager _settingsManager;
    private readonly SearchController _searchController;
    private System.Threading.CancellationTokenSource _cancellationTokenSource = new();
    private DispatcherQueue _updateWindowsQueue = DispatcherQueueController.CreateOnDedicatedThread().DispatcherQueue;

    private bool _disposed;

    public WindowWalkerListPage(SettingsManager settings)
    {
        _settingsManager = settings;
        _searchController = new(_settingsManager);

        Icon = Icons.WindowWalkerIcon;
        Name = Resources.windowwalker_name;
        Id = "com.microsoft.cmdpal.windowwalker";
        PlaceholderText = Resources.windowwalker_PlaceholderText;

        EmptyContent = new CommandItem(new NoOpCommand())
        {
            Icon = Icon,
            Title = Resources.window_walker_top_level_command_title,
            Subtitle = Resources.windowwalker_NoResultsMessage,
        };

        Query(string.Empty);
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _updateWindowsQueue.TryEnqueue(() =>
        {
            Query(newSearch);
        });
    }

    // public List<WindowWalkerListItem> Query(string query)
    public void Query(string query)
    {
        ArgumentNullException.ThrowIfNull(query);

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = new System.Threading.CancellationTokenSource();

        WindowWalkerCommandsProvider.VirtualDesktopHelperInstance.UpdateDesktopList();
        OpenWindows.Instance.UpdateOpenWindowsList(_cancellationTokenSource.Token);
        _searchController.UpdateSearchText(query);
        var searchControllerResults = _searchController.SearchMatches;

        var newListItems = ResultHelper.GetResultList(searchControllerResults, !string.IsNullOrEmpty(query), _settingsManager);
        var oldCount = _results.Count;
        var newCount = newListItems.Count;
        ListHelpers.InPlaceUpdateList(_results, newListItems, out var removedItems);
        if (newCount == oldCount && removedItems.Count == 0)
        {
            // do nothing - windows didn't change
        }
        else
        {
            RaiseItemsChanged(_results.Count);
        }
    }

    public override IListItem[] GetItems()
    {
        return _results.ToArray();
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _cancellationTokenSource?.Dispose();
                _disposed = true;
            }
        }
    }

    internal void RaiseItemsChanged()
    {
        // base.RaiseItemsChanged();
        Query(SearchText);
    }
}
