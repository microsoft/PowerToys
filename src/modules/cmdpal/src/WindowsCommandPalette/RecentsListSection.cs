// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using CmdPal.Models;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using WindowsCommandPalette.Views;

namespace DeveloperCommandPalette;

public sealed class RecentsListSection : ListSection, INotifyCollectionChanged
{
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    private readonly MainViewModel _mainViewModel;

    internal ObservableCollection<MainListItem> _Items { get; set; } = [];

    private bool loadedApps;

    public RecentsListSection(MainViewModel viewModel)
    {
        this.Title = "Recent";
        this._mainViewModel = viewModel;

        var recent = _mainViewModel.RecentActions;
        Reset();
        _Items.CollectionChanged += Bubble_CollectionChanged;

        _mainViewModel.AppsReady += _mainViewModel_AppsReady;
    }

    private void _mainViewModel_AppsReady(object sender, object? args)
    {
        loadedApps = true;
        _mainViewModel.AppsReady -= _mainViewModel_AppsReady;
        AddApps();
    }

    private void Bubble_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            CollectionChanged?.Invoke(this, e);
        });
    }

    public override IListItem[] Items => _Items.ToArray();

    internal void Reset()
    {
        _Items.Clear();
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
            _Items.Add(new MainListItem(app.Unsafe)); // we know these are all local
        }
    }
}
