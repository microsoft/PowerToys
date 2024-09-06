// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using Microsoft.UI.Dispatching;
using WindowsCommandPalette.Views;

namespace DeveloperCommandPalette;

public sealed class RecentsListSection : ListSection, INotifyCollectionChanged
{
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    private readonly MainViewModel _mainViewModel;

    private readonly ObservableCollection<MainListItem> _items = [];

    private bool loadedApps;

    public RecentsListSection(MainViewModel viewModel)
    {
        Title = "Recent";
        _mainViewModel = viewModel;

        var recent = _mainViewModel.RecentActions;
        Reset();
        _items.CollectionChanged += Bubble_CollectionChanged;

        _mainViewModel.AppsReady += MainViewModel_AppsReady;
    }

    private void MainViewModel_AppsReady(object sender, object? args)
    {
        loadedApps = true;
        _mainViewModel.AppsReady -= MainViewModel_AppsReady;

        AddApps();
    }

    private void Bubble_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            CollectionChanged?.Invoke(this, e);
        });
    }

    public override IListItem[] Items => _items.ToArray();

    internal void Reset()
    {
        _items.Clear();
        if (loadedApps)
        {
            AddApps();
        }
    }

    internal void AddApps()
    {
        var apps = _mainViewModel.Recent;
        foreach (var app in apps)
        {
            _items.Add(new MainListItem(app.Unsafe)); // we know these are all local
        }
    }
}
