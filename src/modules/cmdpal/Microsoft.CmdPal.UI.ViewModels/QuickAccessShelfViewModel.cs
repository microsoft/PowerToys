// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CmdPal.Ext.Apps;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed partial class QuickAccessShelfViewModel : ObservableObject, IDisposable
{
    private readonly TopLevelCommandManager _topLevelCommandManager;
    private readonly IAppStateService _appStateService;
    private readonly TaskScheduler _scheduler;
    private readonly List<INotifyPropChanged> _observedItems = [];
    private volatile RecentCommandsManager _recentCommands;
    private volatile bool _includeRecentCommands;
    private volatile bool _isDisposed;
    private int _rebuildQueued;
    private int _visibleCapacity = int.MaxValue;

    public QuickAccessShelfViewModel(
        TopLevelCommandManager topLevelCommandManager,
        IAppStateService appStateService,
        bool includeRecentCommands,
        TaskScheduler scheduler)
    {
        _topLevelCommandManager = topLevelCommandManager;
        _appStateService = appStateService;
        _recentCommands = _appStateService.State.RecentCommands;
        _includeRecentCommands = includeRecentCommands;
        _scheduler = scheduler;
        _topLevelCommandManager.PinnedCommands.CollectionChanged += Commands_CollectionChanged;
        _topLevelCommandManager.TopLevelCommands.CollectionChanged += Commands_CollectionChanged;
        _appStateService.StateChanged += AppStateService_StateChanged;
        AllAppsCommandProvider.Page.PropChanged += AllApps_PropChanged;
        RebuildItems();
    }

    public ObservableCollection<QuickAccessShelfItem> Items { get; } = [];

    public ObservableCollection<QuickAccessShelfItem> VisibleItems { get; } = [];

    public ObservableCollection<QuickAccessShelfItem> OverflowItems { get; } = [];

    public bool HasItems => Items.Count > 0;

    public bool HasOverflow => OverflowItems.Count > 0;

    public int ItemCount => Items.Count;

    public int VisibleItemCount => VisibleItems.Count;

    public void SetIncludeRecentCommands(bool includeRecentCommands)
    {
        if (_includeRecentCommands == includeRecentCommands)
        {
            return;
        }

        _includeRecentCommands = includeRecentCommands;
        QueueRebuild();
    }

    public void SetVisibleCapacity(int capacity)
    {
        capacity = Math.Max(0, capacity);
        if (capacity == _visibleCapacity)
        {
            return;
        }

        _visibleCapacity = capacity;
        RepartitionItems();
    }

    public void UpdateVisibleCapacity(double availableWidth, double itemWidth, double spacing)
    {
        SetVisibleCapacity(QuickAccessShelfResolver.CalculateVisibleCapacity(Items.Count, availableWidth, itemWidth, spacing));
    }

    private void Commands_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        QueueRebuild();
    }

    private void AppStateService_StateChanged(IAppStateService sender, AppStateModel args)
    {
        if (ReferenceEquals(_recentCommands, args.RecentCommands))
        {
            return;
        }

        _recentCommands = args.RecentCommands;
        if (_includeRecentCommands)
        {
            QueueRebuild();
        }
    }

    private void AllApps_PropChanged(object? sender, IPropChangedEventArgs args)
    {
        if (_includeRecentCommands &&
            args.PropertyName == nameof(AllAppsCommandProvider.Page.IsLoading) &&
            !AllAppsCommandProvider.Page.IsLoading)
        {
            QueueRebuild();
        }
    }

    private void ObservedItem_PropChanged(object? sender, IPropChangedEventArgs args)
    {
        if (args.PropertyName is nameof(IListItem.Title) or nameof(IListItem.Icon))
        {
            QueueRebuild();
        }
    }

    private void QueueRebuild()
    {
        if (_isDisposed || Interlocked.Exchange(ref _rebuildQueued, 1) != 0)
        {
            return;
        }

        _ = Task.Factory.StartNew(
            () =>
            {
                Interlocked.Exchange(ref _rebuildQueued, 0);
                if (!_isDisposed)
                {
                    RebuildItems();
                }
            },
            CancellationToken.None,
            TaskCreationOptions.None,
            _scheduler);
    }

    private void RebuildItems()
    {
        var hadItems = HasItems;
        var previousItemCount = ItemCount;

        PinnedCommandSettings[] pinnedCommands;
        lock (_topLevelCommandManager.PinnedCommands)
        {
            pinnedCommands = [.. _topLevelCommandManager.PinnedCommands];
        }

        TopLevelViewModel[] availableCommands;
        lock (_topLevelCommandManager.TopLevelCommands)
        {
            availableCommands = [.. _topLevelCommandManager.TopLevelCommands];
        }

        IEnumerable<string> recentCommandIds = _includeRecentCommands
            ? _recentCommands.EnumerateRecentCommandIds()
            : [];
        var sections = TopLevelCommandResolver.Resolve(
            pinnedCommands,
            recentCommandIds,
            availableCommands,
            includeApps: _includeRecentCommands && _topLevelCommandManager.IsProviderActive(AllAppsCommandProvider.WellKnownId),
            includeRegular: false);

        UpdateObservedItems(sections.Pinned.Concat(sections.Recent));

        var shelfItems = sections.Pinned.Select(
            (command, index) => new QuickAccessShelfItem(command, index, startsRecentSection: false))
            .Concat(sections.Recent.Select(
                (command, index) => new QuickAccessShelfItem(
                    command,
                    shortcutIndex: -1,
                    startsRecentSection: index == 0 && sections.Pinned.Count > 0)));
        ListHelpers.InPlaceUpdateList(Items, shelfItems);
        RepartitionItems();

        if (hadItems != HasItems)
        {
            OnPropertyChanged(nameof(HasItems));
        }

        if (previousItemCount != ItemCount)
        {
            OnPropertyChanged(nameof(ItemCount));
        }
    }

    private void UpdateObservedItems(IEnumerable<IListItem> items)
    {
        foreach (var item in _observedItems)
        {
            item.PropChanged -= ObservedItem_PropChanged;
        }

        _observedItems.Clear();
        foreach (var item in items)
        {
            if (item is INotifyPropChanged observable)
            {
                observable.PropChanged += ObservedItem_PropChanged;
                _observedItems.Add(observable);
            }
        }
    }

    private void RepartitionItems()
    {
        var hadOverflow = HasOverflow;
        var previousVisibleItemCount = VisibleItemCount;
        var visibleItemCount = Math.Min(Items.Count, _visibleCapacity);

        ListHelpers.InPlaceUpdateList(VisibleItems, Items.Take(visibleItemCount));
        ListHelpers.InPlaceUpdateList(OverflowItems, Items.Skip(visibleItemCount));

        if (hadOverflow != HasOverflow)
        {
            OnPropertyChanged(nameof(HasOverflow));
        }

        if (previousVisibleItemCount != VisibleItemCount)
        {
            OnPropertyChanged(nameof(VisibleItemCount));
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _topLevelCommandManager.PinnedCommands.CollectionChanged -= Commands_CollectionChanged;
        _topLevelCommandManager.TopLevelCommands.CollectionChanged -= Commands_CollectionChanged;
        _appStateService.StateChanged -= AppStateService_StateChanged;
        AllAppsCommandProvider.Page.PropChanged -= AllApps_PropChanged;
        UpdateObservedItems([]);
        GC.SuppressFinalize(this);
    }
}
