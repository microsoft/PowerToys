// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed partial class QuickAccessShelfViewModel : ObservableObject, IDisposable
{
    private readonly TopLevelCommandManager _topLevelCommandManager;
    private readonly TaskScheduler _scheduler;
    private volatile bool _isDisposed;
    private int _rebuildQueued;
    private int _visibleCapacity = int.MaxValue;

    public QuickAccessShelfViewModel(TopLevelCommandManager topLevelCommandManager, TaskScheduler scheduler)
    {
        _topLevelCommandManager = topLevelCommandManager;
        _scheduler = scheduler;
        _topLevelCommandManager.PinnedCommands.CollectionChanged += Commands_CollectionChanged;
        _topLevelCommandManager.TopLevelCommands.CollectionChanged += Commands_CollectionChanged;
        RebuildItems();
    }

    public ObservableCollection<QuickAccessShelfItem> Items { get; } = [];

    public ObservableCollection<QuickAccessShelfItem> VisibleItems { get; } = [];

    public ObservableCollection<QuickAccessShelfItem> OverflowItems { get; } = [];

    public bool HasItems => Items.Count > 0;

    public bool HasOverflow => OverflowItems.Count > 0;

    public int ItemCount => Items.Count;

    public int VisibleItemCount => VisibleItems.Count;

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

        var resolvedCommands = QuickAccessShelfResolver.Resolve(
            pinnedCommands,
            availableCommands,
            static command => command.CommandProviderId,
            static command => command.Id,
            TopLevelCommandEligibility.IsEligibleForHome);

        var shelfItems = resolvedCommands.Select((command, index) => new QuickAccessShelfItem(command, index));
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
        GC.SuppressFinalize(this);
    }
}
