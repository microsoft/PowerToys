// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Common;
using Microsoft.CmdPal.Ext.Apps;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed partial class QuickAccessShelfViewModel : ObservableObject, IDisposable
{
    private readonly TopLevelCommandManager _topLevelCommandManager;
    private readonly IAppStateService _appStateService;
    private readonly ISettingsService _settingsService;
    private readonly TaskScheduler _scheduler;
    private readonly Lock _observedItemsLock = new();
    private readonly List<INotifyPropChanged> _observedItems = [];
    private volatile RecentCommandsManager _recentCommands;
    private volatile ShelfConfiguration _configuration;
    private volatile QuickAccessShelfItem[] _itemSnapshot = [];
    private volatile bool _isDisposed;
    private int _rebuildVersion;
    private int _rebuildRunning;
    private int _visibleCapacity = int.MaxValue;

    private sealed record ShelfConfiguration(
        RecentCommandsPlacement RecentCommandsPlacement,
        int PinnedCommandLimit,
        int RecentCommandLimit);

    public QuickAccessShelfViewModel(
        TopLevelCommandManager topLevelCommandManager,
        IAppStateService appStateService,
        ISettingsService settingsService,
        RecentCommandsPlacement recentCommandsPlacement,
        int pinnedCommandLimit,
        int recentCommandLimit,
        TaskScheduler scheduler)
    {
        _topLevelCommandManager = topLevelCommandManager;
        _appStateService = appStateService;
        _settingsService = settingsService;
        _recentCommands = _appStateService.State.RecentCommands;
        _configuration = CreateConfiguration(recentCommandsPlacement, pinnedCommandLimit, recentCommandLimit);
        _scheduler = scheduler;
        _topLevelCommandManager.PinnedCommandsChanged += PinnedCommands_Changed;
        _topLevelCommandManager.TopLevelCommands.CollectionChanged += Commands_CollectionChanged;
        _appStateService.StateChanged += AppStateService_StateChanged;
        AllAppsCommandProvider.Page.PropChanged += AllApps_PropChanged;
        QueueRebuild();
    }

    public ObservableCollection<QuickAccessShelfItem> Items { get; } = [];

    public ObservableCollection<QuickAccessShelfItem> VisibleItems { get; } = [];

    public ObservableCollection<QuickAccessShelfItem> OverflowItems { get; } = [];

    public bool HasItems => Items.Count > 0;

    public bool HasOverflow => OverflowItems.Count > 0;

    public int ItemCount => Items.Count;

    public int VisibleItemCount => VisibleItems.Count;

    public void SetItemConfiguration(
        RecentCommandsPlacement recentCommandsPlacement,
        int pinnedCommandLimit,
        int recentCommandLimit)
    {
        var configuration = CreateConfiguration(recentCommandsPlacement, pinnedCommandLimit, recentCommandLimit);
        if (_configuration == configuration)
        {
            return;
        }

        _configuration = configuration;
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

    public bool TryPlacePinnedItem(QuickAccessShelfItem source, QuickAccessShelfItem target, bool placeAfter)
    {
        if (!target.IsPinned ||
            (source.ProviderId == target.ProviderId && source.CommandId == target.CommandId))
        {
            return false;
        }

        var settings = _settingsService.Settings;
        if (!settings.IsCommandPinned(target.ProviderId, target.CommandId))
        {
            return false;
        }

        var promoted = false;
        if (!settings.IsCommandPinned(source.ProviderId, source.CommandId))
        {
            if (!source.CanPin)
            {
                return false;
            }

            WeakReferenceMessenger.Default.Send(new PinCommandItemMessage(source.ProviderId, source.CommandId));
            promoted = _settingsService.Settings.IsCommandPinned(source.ProviderId, source.CommandId);
            if (!promoted)
            {
                return false;
            }
        }

        var currentSettings = _settingsService.Settings;
        var preview = currentSettings.TryPlacePinnedCommand(
            source.ProviderId,
            source.CommandId,
            target.ProviderId,
            target.CommandId,
            placeAfter);
        var placementChanged = !ReferenceEquals(preview, currentSettings);
        if (placementChanged)
        {
            _settingsService.UpdateSettings(
                settings => settings.TryPlacePinnedCommand(
                    source.ProviderId,
                    source.CommandId,
                    target.ProviderId,
                    target.CommandId,
                    placeAfter),
                hotReload: false);
        }

        if (!promoted && !placementChanged)
        {
            return false;
        }

        _topLevelCommandManager.RebuildPinnedCache();
        return true;
    }

    public bool TryRemoveItem(QuickAccessShelfItem item)
    {
        // Remove from the section the user dragged from. A pinned command may also have a
        // history entry, but unpinning it must not erase that history or its search frecency.
        if (item.IsPinned)
        {
            if (!_settingsService.Settings.IsCommandPinned(item.ProviderId, item.CommandId))
            {
                return false;
            }

            WeakReferenceMessenger.Default.Send(new UnpinCommandItemMessage(item.ProviderId, item.CommandId));

            // The provider can disappear while an item is being dragged. Keep the persisted
            // pin removable even if there is no wrapper left to process the message.
            if (_settingsService.Settings.IsCommandPinned(item.ProviderId, item.CommandId))
            {
                _settingsService.UpdateSettings(
                    settings => settings.TryUnpinCommand(item.ProviderId, item.CommandId),
                    hotReload: false);
            }

            _topLevelCommandManager.RebuildPinnedCache();
            return true;
        }

        var recentCommands = _appStateService.State.RecentCommands;
        var withoutItem = recentCommands.WithoutHistoryItem(item.CommandId);
        if (ReferenceEquals(recentCommands, withoutItem))
        {
            return false;
        }

        _appStateService.UpdateState(state => state with
        {
            RecentCommands = state.RecentCommands.WithoutHistoryItem(item.CommandId),
        });
        return true;
    }

    public bool TryPinItem(QuickAccessShelfItem item)
    {
        if (!item.CanPin || _settingsService.Settings.IsCommandPinned(item.ProviderId, item.CommandId))
        {
            return false;
        }

        WeakReferenceMessenger.Default.Send(new PinCommandItemMessage(item.ProviderId, item.CommandId));
        return _settingsService.Settings.IsCommandPinned(item.ProviderId, item.CommandId);
    }

    private void Commands_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        QueueRebuild();
    }

    private void PinnedCommands_Changed(object? sender, EventArgs e)
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
        if (IncludesRecentCommands(_configuration.RecentCommandsPlacement))
        {
            QueueRebuild();
        }
    }

    private void AllApps_PropChanged(object? sender, IPropChangedEventArgs args)
    {
        if (IncludesRecentCommands(_configuration.RecentCommandsPlacement) &&
            args.PropertyName == nameof(AllAppsCommandProvider.Page.IsLoading) &&
            !AllAppsCommandProvider.Page.IsLoading)
        {
            QueueRebuild();
        }
    }

    private void ObservedItem_PropChanged(object? sender, IPropChangedEventArgs args)
    {
        if (args.PropertyName is nameof(IListItem.Title) or nameof(IListItem.Icon) or nameof(CommandItem.DataPackage))
        {
            QueueRebuild();
        }
    }

    private void QueueRebuild()
    {
        if (_isDisposed)
        {
            return;
        }

        Interlocked.Increment(ref _rebuildVersion);
        TryStartRebuild();
    }

    private void TryStartRebuild()
    {
        if (_isDisposed || Interlocked.CompareExchange(ref _rebuildRunning, 1, 0) != 0)
        {
            return;
        }

        var rebuildVersion = Volatile.Read(ref _rebuildVersion);
        var existingItems = _itemSnapshot;
        _ = Task.Run(() => BuildItems(existingItems)).ContinueWith(
            task => CompleteRebuild(rebuildVersion, task),
            CancellationToken.None,
            TaskContinuationOptions.None,
            _scheduler);
    }

    private QuickAccessShelfItem[] BuildItems(IReadOnlyList<QuickAccessShelfItem> existingItems)
    {
        var configuration = _configuration;
        var includeRecentCommands = IncludesRecentCommands(configuration.RecentCommandsPlacement);

        var pinnedCommands = _topLevelCommandManager.GetPinnedCommandsSnapshot();

        TopLevelViewModel[] availableCommands;
        lock (_topLevelCommandManager.TopLevelCommands)
        {
            availableCommands = [.. _topLevelCommandManager.TopLevelCommands];
        }

        IEnumerable<string> recentCommandIds = includeRecentCommands
            ? _recentCommands.EnumerateRecentCommandIds()
            : [];
        var sections = TopLevelCommandResolver.Resolve(
            pinnedCommands,
            recentCommandIds,
            availableCommands,
            includeApps: includeRecentCommands && _topLevelCommandManager.IsProviderActive(AllAppsCommandProvider.WellKnownId),
            pinnedCommandLimit: configuration.PinnedCommandLimit,
            recentCommandLimit: configuration.RecentCommandLimit,
            includeRegular: false,
            recentCommandsFirst: configuration.RecentCommandsPlacement == RecentCommandsPlacement.BeforePinned);

        var resolvedItems = QuickAccessShelfResolver.ComposeSections(
            sections.Pinned,
            sections.Recent,
            configuration.RecentCommandsPlacement);
        var observedItems = resolvedItems.Select(item => item.Item).ToArray();

        // AppListItem.Icon can publish an async update immediately after it is read below.
        // Observe first so a fast load cannot leave the shelf showing the placeholder icon.
        UpdateObservedItems(observedItems);
        return resolvedItems.Select(
            item =>
            {
                var provider = _topLevelCommandManager.LookupProvider(TopLevelCommandResolver.GetProviderId(item.Item));
                var canPin = !item.IsPinned &&
                    provider is not null &&
                    (item.Item is TopLevelViewModel || provider.SupportsPinning);
                return QuickAccessShelfItem.CreateOrReuse(
                    existingItems,
                    item.Item,
                    item.ShortcutIndex,
                    item.StartsNewSection,
                    item.IsPinned,
                    canPin);
            }).ToArray();
    }

    private void CompleteRebuild(int rebuildVersion, Task<QuickAccessShelfItem[]> task)
    {
        try
        {
            if (task.IsFaulted)
            {
                CoreLogger.LogError("Failed to rebuild quick access shelf", task.Exception.GetBaseException());
            }
            else if (!_isDisposed && rebuildVersion == Volatile.Read(ref _rebuildVersion))
            {
                ApplyRebuild(task.Result);
            }
        }
        finally
        {
            Interlocked.Exchange(ref _rebuildRunning, 0);
            if (!_isDisposed && rebuildVersion != Volatile.Read(ref _rebuildVersion))
            {
                TryStartRebuild();
            }
        }
    }

    private void ApplyRebuild(IReadOnlyList<QuickAccessShelfItem> shelfItems)
    {
        var hadItems = HasItems;
        var previousItemCount = ItemCount;

        ListHelpers.InPlaceUpdateList(Items, shelfItems);
        _itemSnapshot = [.. Items];
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

    private static ShelfConfiguration CreateConfiguration(
        RecentCommandsPlacement recentCommandsPlacement,
        int pinnedCommandLimit,
        int recentCommandLimit)
    {
        if (recentCommandsPlacement is not
            (RecentCommandsPlacement.BeforePinned or RecentCommandsPlacement.AfterPinned))
        {
            recentCommandsPlacement = RecentCommandsPlacement.Hidden;
        }

        return new ShelfConfiguration(
            recentCommandsPlacement,
            Math.Clamp(
                pinnedCommandLimit,
                SettingsModel.MinQuickAccessShelfPinnedCommandLimit,
                SettingsModel.MaxQuickAccessShelfPinnedCommandLimit),
            Math.Clamp(
                recentCommandLimit,
                SettingsModel.MinRecentCommandsDisplayLimit,
                SettingsModel.MaxRecentCommandsDisplayLimit));
    }

    private static bool IncludesRecentCommands(RecentCommandsPlacement recentCommandsPlacement) =>
        recentCommandsPlacement is RecentCommandsPlacement.BeforePinned or RecentCommandsPlacement.AfterPinned;

    private void UpdateObservedItems(IEnumerable<IListItem> items)
    {
        lock (_observedItemsLock)
        {
            foreach (var item in _observedItems)
            {
                item.PropChanged -= ObservedItem_PropChanged;
            }

            _observedItems.Clear();
            if (_isDisposed)
            {
                return;
            }

            foreach (var item in items)
            {
                if (item is INotifyPropChanged observable)
                {
                    observable.PropChanged += ObservedItem_PropChanged;
                    _observedItems.Add(observable);
                }
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
        _topLevelCommandManager.PinnedCommandsChanged -= PinnedCommands_Changed;
        _topLevelCommandManager.TopLevelCommands.CollectionChanged -= Commands_CollectionChanged;
        _appStateService.StateChanged -= AppStateService_StateChanged;
        AllAppsCommandProvider.Page.PropChanged -= AllApps_PropChanged;
        UpdateObservedItems([]);
        GC.SuppressFinalize(this);
    }
}
