// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Threading;
using Microsoft.CmdPal.Common;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.Dock;

#pragma warning disable SA1402 // File may only contain a single type

public sealed partial class DockBandViewModel : ExtensionObjectViewModel
{
    private readonly CommandItemViewModel _rootItem;
    private readonly ISettingsService _settingsService;
    private readonly IContextMenuFactory _contextMenuFactory;
    private readonly Lock _subscriptionLock = new();

    private DockBandSettings _bandSettings;
    private Dictionary<IListItem, DockItemViewModel> _viewModelCache = new(ReferenceEqualityComparer.Instance);
    private InterlockedBoolean _refreshInFlight;
    private InterlockedBoolean _refreshRequested;
    private InterlockedBoolean _cleanupStarted;
    private IListPage? _subscribedList;

    public ObservableCollection<DockItemViewModel> Items { get; } = new();

    private bool _showTitles = true;
    private bool _showSubtitles = true;
    private bool? _showTitlesSnapshot;
    private bool? _showSubtitlesSnapshot;

    public string Id => _rootItem.Command.Id;

    /// <summary>
    /// Gets or sets a value indicating whether titles are shown for items in this band.
    /// This is a preview value - call <see cref="SaveLabelSettings"/> to persist or
    /// <see cref="RestoreLabelSettings"/> to discard changes.
    /// </summary>
    public bool ShowTitles
    {
        get => _showTitles;
        set
        {
            if (_showTitles != value)
            {
                _showTitles = value;
                foreach (var item in Items)
                {
                    item.ShowTitle = value;
                }

                UpdateProperty(nameof(ShowTitles));
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether subtitles are shown for items in this band.
    /// This is a preview value - call <see cref="SaveLabelSettings"/> to persist or
    /// <see cref="RestoreLabelSettings"/> to discard changes.
    /// </summary>
    public bool ShowSubtitles
    {
        get => _showSubtitles;
        set
        {
            if (_showSubtitles != value)
            {
                _showSubtitles = value;
                foreach (var item in Items)
                {
                    item.ShowSubtitle = value;
                }

                UpdateProperty(nameof(ShowSubtitles));
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether labels (both titles and subtitles) are shown.
    /// Provided for backward compatibility - setting this sets both ShowTitles and ShowSubtitles.
    /// </summary>
    public bool ShowLabels
    {
        get => _showTitles && _showSubtitles;
        set
        {
            ShowTitles = value;
            ShowSubtitles = value;
        }
    }

    /// <summary>
    /// Takes a snapshot of the current label settings before editing.
    /// </summary>
    internal void SnapshotShowLabels()
    {
        _showTitlesSnapshot = _showTitles;
        _showSubtitlesSnapshot = _showSubtitles;
    }

    /// <summary>
    /// Saves the current label settings to settings.
    /// </summary>
    /// <returns>The band settings containing any saved label changes.</returns>
    internal DockBandSettings SaveShowLabels()
    {
        // Only write to settings if the label values actually changed from
        // the snapshot. When multiple non-customized monitors share global
        // bands, an unconditional save would overwrite changes made by
        // another monitor's ViewModel (last-save-wins clobber).
        var changed = _showTitlesSnapshot is null
                   || _showTitles != _showTitlesSnapshot
                   || _showSubtitles != _showSubtitlesSnapshot;
        if (changed)
        {
            ReplaceBandInSettings(_bandSettings with { ShowTitles = _showTitles, ShowSubtitles = _showSubtitles });
        }

        _showTitlesSnapshot = null;
        _showSubtitlesSnapshot = null;
        return _bandSettings;
    }

    /// <summary>
    /// Restores the label settings from the snapshot.
    /// </summary>
    internal void RestoreShowLabels()
    {
        if (_showTitlesSnapshot.HasValue)
        {
            ShowTitles = _showTitlesSnapshot.Value;
            _showTitlesSnapshot = null;
        }

        if (_showSubtitlesSnapshot.HasValue)
        {
            ShowSubtitles = _showSubtitlesSnapshot.Value;
            _showSubtitlesSnapshot = null;
        }
    }

    private void ReplaceBandInSettings(DockBandSettings newSettings)
    {
        var commandId = _bandSettings.CommandId;
        _settingsService.UpdateSettings(
            s =>
                {
                    var dockSettings = s.DockSettings;

                    // Update in global bands
                    var updatedDock = dockSettings with
                    {
                        StartBands = ReplaceBandInList(dockSettings.StartBands, commandId, newSettings),
                        CenterBands = ReplaceBandInList(dockSettings.CenterBands, commandId, newSettings),
                        EndBands = ReplaceBandInList(dockSettings.EndBands, commandId, newSettings),
                    };

                    // Also update in per-monitor bands for customized monitors
                    var configs = updatedDock.MonitorConfigs ?? ImmutableList<DockMonitorConfig>.Empty;
                    var configsChanged = false;
                    for (var i = 0; i < configs.Count; i++)
                    {
                        var config = configs[i];
                        if (!config.IsCustomized)
                        {
                            continue;
                        }

                        var start = config.StartBands ?? ImmutableList<DockBandSettings>.Empty;
                        var center = config.CenterBands ?? ImmutableList<DockBandSettings>.Empty;
                        var end = config.EndBands ?? ImmutableList<DockBandSettings>.Empty;

                        var newStart = ReplaceBandInList(start, commandId, newSettings);
                        var newCenter = ReplaceBandInList(center, commandId, newSettings);
                        var newEnd = ReplaceBandInList(end, commandId, newSettings);

                        if (newStart != start || newCenter != center || newEnd != end)
                        {
                            configs = configs.SetItem(i, config with
                            {
                                StartBands = newStart,
                                CenterBands = newCenter,
                                EndBands = newEnd,
                            });
                            configsChanged = true;
                        }
                    }

                    if (configsChanged)
                    {
                        updatedDock = updatedDock with { MonitorConfigs = configs };
                    }

                    return s with { DockSettings = updatedDock };
                },
            false);
        _bandSettings = newSettings;
    }

    private static ImmutableList<DockBandSettings> ReplaceBandInList(ImmutableList<DockBandSettings> list, string commandId, DockBandSettings newSettings)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].CommandId == commandId)
            {
                return list.SetItem(i, newSettings);
            }
        }

        return list;
    }

    internal DockBandViewModel(
        CommandItemViewModel commandItemViewModel,
        WeakReference<IPageContext> errorContext,
        DockBandSettings settings,
        ISettingsService settingsService,
        IContextMenuFactory contextMenuFactory)
        : base(errorContext)
    {
        _rootItem = commandItemViewModel;
        _bandSettings = settings;
        _settingsService = settingsService;
        _contextMenuFactory = contextMenuFactory;

        var dockSettings = settingsService.Settings.DockSettings;
        _showTitles = settings.ResolveShowTitles(dockSettings.ShowLabels);
        _showSubtitles = settings.ResolveShowSubtitles(dockSettings.ShowLabels);
    }

    public static ICommandItem[] GetItemsForDisplay(CommandItemViewModel rootItem)
    {
        if (rootItem.Command.Model.Unsafe is IListPage list)
        {
            return list.GetItems();
        }

        return rootItem.Model.Unsafe is ICommandItem item ? [item] : [];
    }

    private void InitializeFromList(IListPage list)
    {
        if (_cleanupStarted.Value)
        {
            return;
        }

        _refreshRequested.Set();
        if (!_refreshInFlight.Set())
        {
            return;
        }

        BuildRefresh(list);
    }

    private void BuildRefresh(IListPage list)
    {
        List<DockItemViewModel> createdViewModels = [];
        try
        {
            _refreshRequested.Clear();
            if (_cleanupStarted.Value)
            {
                CompleteRefresh(list);
                return;
            }

            var items = list.GetItems();
            var currentCache = Volatile.Read(ref _viewModelCache);
            var nextCache = new Dictionary<IListItem, DockItemViewModel>(items.Length, ReferenceEqualityComparer.Instance);
            var newViewModels = new List<DockItemViewModel>(items.Length);

            foreach (var item in items)
            {
                if (item is null)
                {
                    continue;
                }

                if (!nextCache.TryGetValue(item, out var itemViewModel) &&
                    !currentCache.TryGetValue(item, out itemViewModel))
                {
                    itemViewModel = new DockItemViewModel(new(item), PageContext, _showTitles, _showSubtitles, _contextMenuFactory);
                    createdViewModels.Add(itemViewModel);
                    itemViewModel.SlowInitializeProperties();
                }

                nextCache[item] = itemViewModel;
                newViewModels.Add(itemViewModel);
            }

            if (!TryDoOnUiThread(() => ApplyRefresh(list, nextCache, newViewModels, createdViewModels)))
            {
                QueueCleanup(createdViewModels);
                CompleteRefresh(list);
            }
        }
        catch
        {
            // Clear the gate directly rather than through CompleteRefresh: any pending _refreshRequested is dropped
            // on purpose, because re-arming against a GetItems() that keeps throwing would spin a tight retry loop.
            // The next ItemsChanged starts a fresh refresh.
            QueueCleanup(createdViewModels);
            _refreshInFlight.Clear();
            throw;
        }
    }

    private void ApplyRefresh(
        IListPage list,
        Dictionary<IListItem, DockItemViewModel> nextCache,
        IReadOnlyList<DockItemViewModel> newViewModels,
        IReadOnlyList<DockItemViewModel> createdViewModels)
    {
        List<DockItemViewModel> removedItems = [];
        try
        {
            if (_cleanupStarted.Value)
            {
                return;
            }

            foreach (var viewModel in newViewModels)
            {
                viewModel.ShowTitle = _showTitles;
                viewModel.ShowSubtitle = _showSubtitles;
            }

            ListHelpers.InPlaceUpdateList(Items, newViewModels, out removedItems);
            Volatile.Write(ref _viewModelCache, nextCache);
        }
        finally
        {
            CleanupDiscardedViewModels(removedItems, createdViewModels);
            CompleteRefresh(list);
        }
    }

    private void CompleteRefresh(IListPage list)
    {
        _refreshInFlight.Clear();
        if (_cleanupStarted.Value ||
            !_refreshRequested.Value)
        {
            return;
        }

        _ = Task.Run(
            () =>
            {
                try
                {
                    InitializeFromList(list);
                }
                catch (Exception ex)
                {
                    CoreLogger.LogError("Failed to refresh a Dock band.", ex);
                }
            });
    }

    private void CleanupDiscardedViewModels(
        IEnumerable<DockItemViewModel> removedItems,
        IEnumerable<DockItemViewModel> createdViewModels)
    {
        var retained = new HashSet<DockItemViewModel>(Items, ReferenceEqualityComparer.Instance);
        var discarded = new HashSet<DockItemViewModel>(ReferenceEqualityComparer.Instance);

        foreach (var item in removedItems)
        {
            if (!retained.Contains(item))
            {
                discarded.Add(item);
            }
        }

        foreach (var item in createdViewModels)
        {
            if (!retained.Contains(item))
            {
                discarded.Add(item);
            }
        }

        QueueCleanup(discarded);
    }

    private static void QueueCleanup(IEnumerable<DockItemViewModel> viewModels)
    {
        var pendingCleanup = viewModels.ToArray();
        if (pendingCleanup.Length != 0)
        {
            _ = Task.Run(
                () =>
                {
                    foreach (var viewModel in pendingCleanup)
                    {
                        viewModel.SafeCleanup();
                    }
                });
        }
    }

    public override void InitializeProperties()
    {
        if (_cleanupStarted.Value)
        {
            return;
        }

        var command = _rootItem.Command;
        var list = command.Model.Unsafe as IListPage;
        if (list is not null)
        {
            lock (_subscriptionLock)
            {
                if (_cleanupStarted.Value || _subscribedList is not null)
                {
                    return;
                }

                list.ItemsChanged += HandleItemsChanged;
                _subscribedList = list;
            }

            if (_cleanupStarted.Value)
            {
                return;
            }

            InitializeFromList(list);
        }
        else
        {
            var dockItem = new DockItemViewModel(_rootItem, _showTitles, _showSubtitles, _contextMenuFactory);
            dockItem.SlowInitializeProperties();
            if (!TryDoOnUiThread(() =>
            {
                if (!_cleanupStarted.Value)
                {
                    Items.Add(dockItem);
                }
                else
                {
                    QueueCleanup([dockItem]);
                }
            }))
            {
                QueueCleanup([dockItem]);
            }
        }
    }

    private void HandleItemsChanged(object sender, IItemsChangedEventArgs args)
    {
        if (_cleanupStarted.Value)
        {
            return;
        }

        if (_rootItem.Command.Model.Unsafe is IListPage p)
        {
            InitializeFromList(p);
        }
    }

    protected override void UnsafeCleanup()
    {
        if (!_cleanupStarted.Set())
        {
            return;
        }

        base.UnsafeCleanup();

        IListPage? subscribedList;
        lock (_subscriptionLock)
        {
            subscribedList = _subscribedList;
            _subscribedList = null;
        }

        if (subscribedList is not null)
        {
            subscribedList.ItemsChanged -= HandleItemsChanged;
        }

        Volatile.Write(
            ref _viewModelCache,
            new Dictionary<IListItem, DockItemViewModel>(ReferenceEqualityComparer.Instance));

        if (!TryDoOnUiThread(DrainItems))
        {
            QueueCleanup(Items.ToArray());
        }
    }

    private void DrainItems()
    {
        var items = Items.ToArray();
        Items.Clear();
        QueueCleanup(items);
    }
}

public partial class DockItemViewModel : CommandItemViewModel
{
    private bool _showTitle = true;
    private bool _showSubtitle = true;

    public bool ShowTitle
    {
        get => _showTitle;
        internal set
        {
            if (_showTitle != value)
            {
                _showTitle = value;
                UpdateProperty(nameof(ShowTitle));
                UpdateProperty(nameof(ShowLabel));
                UpdateProperty(nameof(HasText));
                UpdateProperty(nameof(Title));
            }
        }
    }

    public bool ShowSubtitle
    {
        get => _showSubtitle;
        internal set
        {
            if (_showSubtitle != value)
            {
                _showSubtitle = value;
                UpdateProperty(nameof(ShowSubtitle));
                UpdateProperty(nameof(ShowLabel));
                UpdateProperty(nameof(Subtitle));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether labels are shown (either titles or subtitles).
    /// Setting this sets both ShowTitle and ShowSubtitle.
    /// </summary>
    public bool ShowLabel
    {
        get => _showTitle || _showSubtitle;
        internal set
        {
            ShowTitle = value;
            ShowSubtitle = value;
        }
    }

    public override string Title => _showTitle ? ItemTitle : string.Empty;

    public override string Subtitle => _showSubtitle ? base.Subtitle : string.Empty;

    public override bool HasText => (_showTitle && !string.IsNullOrEmpty(ItemTitle)) || (_showSubtitle && !string.IsNullOrEmpty(base.Subtitle));

    /// <summary>
    /// Gets the tooltip for the dock item, which includes the title and
    /// subtitle. If it doesn't have one part, it just returns the other.
    /// </summary>
    /// <remarks>
    /// Trickery: in the case one is empty, we can just concatenate, and it will
    /// always only be the one that's non-empty
    /// </remarks>
    public string Tooltip =>
        !string.IsNullOrEmpty(ItemTitle) && !string.IsNullOrEmpty(base.Subtitle) ?
            $"{ItemTitle}\n{base.Subtitle}" :
            ItemTitle + base.Subtitle;

    public DockItemViewModel(CommandItemViewModel root, bool showTitle, bool showSubtitle, IContextMenuFactory contextMenuFactory)
        : this(root.Model, root.PageContext, showTitle, showSubtitle, contextMenuFactory)
    {
    }

    public DockItemViewModel(ExtensionObject<ICommandItem> item, WeakReference<IPageContext> errorContext, bool showTitle, bool showSubtitle, IContextMenuFactory contextMenuFactory)
        : base(item, errorContext, contextMenuFactory)
    {
        _showTitle = showTitle;
        _showSubtitle = showSubtitle;
        PropertyChanged += (s, e) =>
        {
            if (e.PropertyName is nameof(Title) or nameof(Subtitle))
            {
                UpdateProperty(nameof(Tooltip));
            }
        };
    }

    public override bool Equals(object? obj) => obj is DockItemViewModel viewModel && viewModel.Model.Equals(Model);

    public override int GetHashCode() => Model.GetHashCode();
}


#pragma warning restore SA1402 // File may only contain a single type
