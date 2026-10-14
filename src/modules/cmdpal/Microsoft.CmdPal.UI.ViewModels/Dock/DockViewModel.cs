// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.Dock;

public sealed partial class DockViewModel : IDisposable
{
    private readonly TopLevelCommandManager _topLevelCommandManager;
    private readonly ISettingsService _settingsService;
    private readonly DockPageContext _pageContext; // only to be used for our own context menu - not for dock bands themselves
    private readonly IContextMenuFactory _contextMenuFactory;
    private readonly string? _monitorDeviceId;

    // Only touched from the UI scheduler (ApplyBands).
    private readonly HashSet<string> _reportedMissingBands = new(StringComparer.Ordinal);

    private DockSettings _settings;
    private bool _isEditing;
    private bool _disposed;
    private int _setupBandsQueued;

    /// <summary>
    /// Gets the monitor device identifier this dock is associated with, or <c>null</c>
    /// for the default (single-monitor) dock.
    /// </summary>
    public string? MonitorDeviceId => _monitorDeviceId;

    public TaskScheduler Scheduler { get; }

    public ObservableCollection<DockBandViewModel> StartItems { get; } = new();

    public ObservableCollection<DockBandViewModel> CenterItems { get; } = new();

    public ObservableCollection<DockBandViewModel> EndItems { get; } = new();

    public IReadOnlyList<TopLevelViewModel> AllItems => _topLevelCommandManager.GetDockBandsSnapshot();

    public DockViewModel(
        TopLevelCommandManager tlcManager,
        IContextMenuFactory contextMenuFactory,
        TaskScheduler scheduler,
        ISettingsService settingsService,
        string? monitorDeviceId = null)
    {
        _topLevelCommandManager = tlcManager;
        _contextMenuFactory = contextMenuFactory;
        _settingsService = settingsService;
        _settings = _settingsService.Settings.DockSettings;
        _monitorDeviceId = monitorDeviceId;
        Scheduler = scheduler;
        _pageContext = new(this);

        _topLevelCommandManager.DockBands.CollectionChanged += DockBands_CollectionChanged;
        _topLevelCommandManager.PropertyChanged += TopLevelCommandManager_PropertyChanged;

        EmitDockConfiguration();
    }

    private void TopLevelCommandManager_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Once loading finishes, do one more (cheap) pass so that any pinned
        // bands that still couldn't be found get reported.
        if (e.PropertyName == nameof(TopLevelCommandManager.IsLoading) &&
            !_topLevelCommandManager.IsLoading &&
            !_isEditing)
        {
            SetupBands();
        }
    }

    private void DockBands_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (_isEditing)
        {
            return;
        }

        // Refresh settings so newly pinned/unpinned bands are visible.
        // Pin/unpin operations save with hotReload:false (to avoid
        // double-updates), so _settings can be stale here.
        _settings = _settingsService.Settings.DockSettings;
        SetupBands();
    }

    public void UpdateSettings(DockSettings settings)
    {
        if (_isEditing)
        {
            return;
        }

        if (_settings == settings)
        {
            return;
        }

        _settings = settings;
        SetupBands();
    }

    /// <summary>
    /// Initializes bands from current settings. Call after the UI scheduler is ready
    /// (i.e., after the DockWindow is shown) to ensure proper dispatcher access.
    /// </summary>
    public void InitializeBands() => SetupBands();

    /// <summary>
    /// Gets the active band lists for this dock instance. Returns per-monitor bands
    /// when the associated monitor is customized; otherwise falls back to global bands.
    /// </summary>
    private (ImmutableList<DockBandSettings> Start, ImmutableList<DockBandSettings> Center, ImmutableList<DockBandSettings> End) GetActiveBands()
    {
        if (_monitorDeviceId is not null)
        {
            var config = FindMonitorConfig(_settings, _monitorDeviceId);
            if (config is not null)
            {
                return (
                    config.ResolveStartBands(_settings.StartBands),
                    config.ResolveCenterBands(_settings.CenterBands),
                    config.ResolveEndBands(_settings.EndBands));
            }
        }

        return (_settings.StartBands, _settings.CenterBands, _settings.EndBands);
    }

    /// <summary>
    /// Returns an updated <see cref="DockSettings"/> with the given bands placed in the
    /// correct location — per-monitor config when customized, or global otherwise.
    /// </summary>
    private DockSettings WithActiveBands(
        ImmutableList<DockBandSettings> start,
        ImmutableList<DockBandSettings> center,
        ImmutableList<DockBandSettings> end)
    {
        if (_monitorDeviceId is not null)
        {
            var config = FindMonitorConfig(_settings, _monitorDeviceId);
            if (config is not null && config.IsCustomized)
            {
                var updatedConfig = config with
                {
                    StartBands = start,
                    CenterBands = center,
                    EndBands = end,
                };
                return _settings with
                {
                    MonitorConfigs = ReplaceMonitorConfig(_settings.MonitorConfigs, updatedConfig),
                };
            }
        }

        return _settings with
        {
            StartBands = start,
            CenterBands = center,
            EndBands = end,
        };
    }

    /// <summary>
    /// Ensures the monitor associated with this dock has its own independent band lists.
    /// If the monitor is not yet customized, forks bands from global settings.
    /// Returns <c>true</c> if the fork was performed, <c>false</c> if already customized or no monitor.
    /// </summary>
    public bool EnsureMonitorForked()
    {
        if (_monitorDeviceId is null)
        {
            return false;
        }

        var config = FindMonitorConfig(_settings, _monitorDeviceId);
        if (config is null || config.IsCustomized)
        {
            return false;
        }

        var forked = config.ForkFromGlobal(_settings);
        _settings = _settings with
        {
            MonitorConfigs = ReplaceMonitorConfig(_settings.MonitorConfigs, forked),
        };
        SaveSettings();
        return true;
    }

    /// <summary>
    /// Gets the effective dock side for this instance, considering per-monitor overrides.
    /// </summary>
    public DockSide GetEffectiveSide()
    {
        if (_monitorDeviceId is not null)
        {
            var config = FindMonitorConfig(_settings, _monitorDeviceId);
            if (config is not null)
            {
                return config.ResolveSide(_settings.Side);
            }
        }

        return _settings.Side;
    }

    private static DockMonitorConfig? FindMonitorConfig(DockSettings settings, string deviceId)
    {
        var configs = settings.MonitorConfigs ?? System.Collections.Immutable.ImmutableList<DockMonitorConfig>.Empty;
        foreach (var config in configs)
        {
            if (string.Equals(config.MonitorDeviceId, deviceId, System.StringComparison.OrdinalIgnoreCase))
            {
                return config;
            }
        }

        return null;
    }

    private static ImmutableList<DockMonitorConfig> ReplaceMonitorConfig(
        ImmutableList<DockMonitorConfig> configs,
        DockMonitorConfig updated)
    {
        for (var i = 0; i < configs.Count; i++)
        {
            if (string.Equals(configs[i].MonitorDeviceId, updated.MonitorDeviceId, System.StringComparison.OrdinalIgnoreCase))
            {
                return configs.SetItem(i, updated);
            }
        }

        return configs.Add(updated);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _topLevelCommandManager.DockBands.CollectionChanged -= DockBands_CollectionChanged;
            _topLevelCommandManager.PropertyChanged -= TopLevelCommandManager_PropertyChanged;
            _disposed = true;
        }
    }

    /// <summary>
    /// Queues a rebuild of the band collections from the current settings and
    /// the currently loaded dock bands.
    /// </summary>
    /// <remarks>
    /// During startup, every provider that finishes loading changes
    /// <see cref="TopLevelCommandManager.DockBands"/>, and every change calls
    /// this. Rebuilds are coalesced: if one is already queued, it will pick up
    /// the latest state when it runs, so a burst of changes only costs one
    /// rebuild. The rebuild itself reuses existing band view models whenever
    /// possible, so only bands that are actually new get initialized (which
    /// is the expensive, cross-process part).
    /// </remarks>
    private void SetupBands()
    {
        if (Interlocked.Exchange(ref _setupBandsQueued, 1) == 1)
        {
            return;
        }

        DoOnUiThread(() =>
        {
            // Clear the flag before reading any state, so that changes that
            // happen while we're applying will queue another pass.
            Volatile.Write(ref _setupBandsQueued, 0);
            ApplyBands();
        });
    }

    private void ApplyBands()
    {
        if (_isEditing || _disposed)
        {
            return;
        }

        var (start, center, end) = GetActiveBands();
        var defaultShowLabels = _settingsService.Settings.DockSettings.ShowLabels;

        var loadedBands = new Dictionary<string, TopLevelViewModel>(StringComparer.Ordinal);
        foreach (var band in _topLevelCommandManager.GetDockBandsSnapshot())
        {
            loadedBands.TryAdd(band.Id, band);
        }

        var existingBands = new Dictionary<string, DockBandViewModel>(StringComparer.Ordinal);
        var oldBands = new List<DockBandViewModel>();
        foreach (var band in StartItems.Concat(CenterItems).Concat(EndItems))
        {
            oldBands.Add(band);
            existingBands.TryAdd(band.Id, band);
        }

        var keptBands = new HashSet<DockBandViewModel>(ReferenceEqualityComparer.Instance);
        var createdBands = new List<DockBandViewModel>();

        List<DockBandViewModel> BuildBands(ImmutableList<DockBandSettings> bands)
        {
            List<DockBandViewModel> result = new();
            foreach (var band in bands)
            {
                var commandId = band.CommandId;
                if (!loadedBands.TryGetValue(commandId, out var topLevelCommand))
                {
                    ReportMissingBand(band);
                    continue;
                }

                _reportedMissingBands.Remove(commandId);

                var itemViewModel = topLevelCommand.ItemViewModel;
                if (existingBands.TryGetValue(commandId, out var existing) &&
                    existing.CanReuseFor(itemViewModel, band, defaultShowLabels) &&
                    keptBands.Add(existing))
                {
                    result.Add(existing);
                    continue;
                }

                // note: CreateBandItem doesn't actually initialize the band, it
                // just creates the VM. We initialize the new ones on a BG
                // thread below.
                var bandVm = CreateBandItem(band, itemViewModel);
                keptBands.Add(bandVm);
                createdBands.Add(bandVm);
                result.Add(bandVm);
            }

            return result;
        }

        var newStart = BuildBands(start);
        var newCenter = BuildBands(center);
        var newEnd = BuildBands(end);

        ListHelpers.InPlaceUpdateList(StartItems, newStart);
        ListHelpers.InPlaceUpdateList(CenterItems, newCenter);
        ListHelpers.InPlaceUpdateList(EndItems, newEnd);

        // A band that moved between sections is kept, so only clean up the
        // ones that aren't in any section anymore.
        var removedBands = oldBands.Where(b => !keptBands.Contains(b)).ToList();

        if (createdBands.Count == 0 && removedBands.Count == 0)
        {
            return;
        }

        Logger.LogDebug($"Dock bands updated: {newStart.Count} start, {newCenter.Count} center, {newEnd.Count} end; {createdBands.Count} created, {removedBands.Count} removed");

        Task.Run(() =>
        {
            foreach (var removedItem in removedBands)
            {
                removedItem.SafeCleanup();
            }

            foreach (var band in createdBands)
            {
                band.SafeInitializePropertiesSynchronous();
            }
        });
    }

    private void ReportMissingBand(DockBandSettings band)
    {
        // While extensions are still loading, a pinned band that isn't loaded
        // yet is expected - its provider just hasn't finished. Only warn once
        // loading is done, and only once per band.
        if (_topLevelCommandManager.IsLoading)
        {
            Logger.LogDebug($"[DockDrop] DockViewModel.SetupBands: band command '{band.CommandId}' (provider='{band.ProviderId}') is not loaded yet");
        }
        else if (_reportedMissingBands.Add(band.CommandId))
        {
            Logger.LogWarning($"[DockDrop] DockViewModel.SetupBands: failed to find band command '{band.CommandId}' (provider='{band.ProviderId}')");
        }
    }

    /// <summary>
    /// Instantiate a new band view model for this CommandItem, given the
    /// settings. The DockBandViewModel will _not_ be initialized - callers
    /// will need to make sure to initialize it somewhere else (off the UI
    /// thread)
    /// </summary>
    private DockBandViewModel CreateBandItem(
        DockBandSettings bandSettings,
        CommandItemViewModel commandItem)
    {
        DockBandViewModel band = new(commandItem, commandItem.PageContext, bandSettings, _settingsService, _contextMenuFactory);

        // the band is NOT initialized here!
        return band;
    }

    private void SaveSettings()
    {
        _settingsService.UpdateSettings(s => s with { DockSettings = _settings });
    }

    public DockBandViewModel? FindBandByTopLevel(TopLevelViewModel tlc)
    {
        var id = tlc.Id;
        return FindBandById(id);
    }

    public DockBandViewModel? FindBandById(string id)
    {
        foreach (var band in StartItems)
        {
            if (band.Id == id)
            {
                return band;
            }
        }

        foreach (var band in CenterItems)
        {
            if (band.Id == id)
            {
                return band;
            }
        }

        foreach (var band in EndItems)
        {
            if (band.Id == id)
            {
                return band;
            }
        }

        return null;
    }

    /// <summary>
    /// Syncs the band position in settings after a same-list reorder.
    /// Does not save to disk - call SaveBandOrder() when done editing.
    /// </summary>
    public void SyncBandPosition(DockBandViewModel band, DockPinSide targetSide, int targetIndex)
    {
        var bandId = band.Id;
        var (activeStart, activeCenter, activeEnd) = GetActiveBands();

        var bandSettings = activeStart.FirstOrDefault(b => b.CommandId == bandId)
                        ?? activeCenter.FirstOrDefault(b => b.CommandId == bandId)
                        ?? activeEnd.FirstOrDefault(b => b.CommandId == bandId);

        if (bandSettings == null)
        {
            return;
        }

        // Remove from all active band lists
        var newStart = activeStart.RemoveAll(b => b.CommandId == bandId);
        var newCenter = activeCenter.RemoveAll(b => b.CommandId == bandId);
        var newEnd = activeEnd.RemoveAll(b => b.CommandId == bandId);

        // Add to target list at the correct index
        var targetList = targetSide switch
        {
            DockPinSide.Start => newStart,
            DockPinSide.Center => newCenter,
            DockPinSide.End => newEnd,
            _ => newStart,
        };
        var insertIndex = Math.Min(targetIndex, targetList.Count);
        switch (targetSide)
        {
            case DockPinSide.Start:
                newStart = newStart.Insert(insertIndex, bandSettings);
                break;
            case DockPinSide.Center:
                newCenter = newCenter.Insert(insertIndex, bandSettings);
                break;
            case DockPinSide.End:
            default:
                newEnd = newEnd.Insert(insertIndex, bandSettings);
                break;
        }

        _settings = WithActiveBands(newStart, newCenter, newEnd);
    }

    /// <summary>
    /// Moves a dock band to a new position (cross-list drop).
    /// Does not save to disk - call SaveBandOrder() when done editing.
    /// </summary>
    public void MoveBandWithoutSaving(DockBandViewModel band, DockPinSide targetSide, int targetIndex)
    {
        var bandId = band.Id;
        var (activeStart, activeCenter, activeEnd) = GetActiveBands();

        var bandSettings = activeStart.FirstOrDefault(b => b.CommandId == bandId)
                        ?? activeCenter.FirstOrDefault(b => b.CommandId == bandId)
                        ?? activeEnd.FirstOrDefault(b => b.CommandId == bandId);

        if (bandSettings == null)
        {
            Logger.LogWarning($"Could not find band settings for band {bandId}");
            return;
        }

        // Remove from all sides (settings)
        var newStart = activeStart.RemoveAll(b => b.CommandId == bandId);
        var newCenter = activeCenter.RemoveAll(b => b.CommandId == bandId);
        var newEnd = activeEnd.RemoveAll(b => b.CommandId == bandId);

        // Remove from UI collections
        StartItems.Remove(band);
        CenterItems.Remove(band);
        EndItems.Remove(band);

        // Add to the target side at the specified index
        switch (targetSide)
        {
            case DockPinSide.Start:
                {
                    var settingsIndex = Math.Min(targetIndex, newStart.Count);
                    newStart = newStart.Insert(settingsIndex, bandSettings);

                    var uiIndex = Math.Min(targetIndex, StartItems.Count);
                    StartItems.Insert(uiIndex, band);
                    break;
                }

            case DockPinSide.Center:
                {
                    var settingsIndex = Math.Min(targetIndex, newCenter.Count);
                    newCenter = newCenter.Insert(settingsIndex, bandSettings);

                    var uiIndex = Math.Min(targetIndex, CenterItems.Count);
                    CenterItems.Insert(uiIndex, band);
                    break;
                }

            case DockPinSide.End:
                {
                    var settingsIndex = Math.Min(targetIndex, newEnd.Count);
                    newEnd = newEnd.Insert(settingsIndex, bandSettings);

                    var uiIndex = Math.Min(targetIndex, EndItems.Count);
                    EndItems.Insert(uiIndex, band);
                    break;
                }
        }

        _settings = WithActiveBands(newStart, newCenter, newEnd);

        Logger.LogDebug($"Moved band {bandId} to {targetSide} at index {targetIndex} (not saved yet)");
    }

    /// <summary>
    /// Saves the current band order and label settings to settings.
    /// Call this when exiting edit mode.
    /// </summary>
    public void SaveBandOrder()
    {
        var pendingBandSettings = new Dictionary<string, DockBandSettings>(StringComparer.Ordinal);
        foreach (var band in StartItems.Concat(CenterItems).Concat(EndItems))
        {
            var settings = band.SaveShowLabels();
            pendingBandSettings[settings.CommandId] = settings;
        }

        // Preserve any per-band label edits made while in edit mode. Those edits are
        // saved independently of reorder, so merge the latest band settings back into
        // the local reordered snapshot before we persist dock settings.
        var (latestStart, latestCenter, latestEnd) = GetActiveBandsFromSettings(_settingsService.Settings.DockSettings);
        var latestBandSettings = BuildBandSettingsLookup(latestStart, latestCenter, latestEnd);
        var (activeStart, activeCenter, activeEnd) = GetActiveBands();
        _settings = WithActiveBands(
            MergeBandSettings(activeStart, latestBandSettings, pendingBandSettings),
            MergeBandSettings(activeCenter, latestBandSettings, pendingBandSettings),
            MergeBandSettings(activeEnd, latestBandSettings, pendingBandSettings));

        _snapshotDockSettings = null;
        _snapshotBandViewModels = null;

        // Extract the final merged bands for this monitor
        var (myStart, myCenter, myEnd) = GetActiveBands();

        // Save only this monitor's bands into the CURRENT persisted settings,
        // preserving other monitors' changes. Without this, each DockViewModel's
        // save would overwrite the entire DockSettings, causing the last save to
        // clobber changes from monitors that saved earlier.
        _settingsService.UpdateSettings(
            s =>
            {
                var currentDock = s.DockSettings;
                if (_monitorDeviceId is not null)
                {
                    var config = FindMonitorConfig(currentDock, _monitorDeviceId);
                    if (config is not null && config.IsCustomized)
                    {
                        var updatedConfig = config with
                        {
                            StartBands = myStart,
                            CenterBands = myCenter,
                            EndBands = myEnd,
                        };
                        var configs = currentDock.MonitorConfigs ?? ImmutableList<DockMonitorConfig>.Empty;
                        return s with
                        {
                            DockSettings = currentDock with
                            {
                                MonitorConfigs = ReplaceMonitorConfig(configs, updatedConfig),
                            },
                        };
                    }
                }

                return s with
                {
                    DockSettings = currentDock with
                    {
                        StartBands = myStart,
                        CenterBands = myCenter,
                        EndBands = myEnd,
                    },
                };
            },
            false);

        // Refresh local settings from persisted state so all monitors' changes are visible
        _settings = _settingsService.Settings.DockSettings;
        _isEditing = false;
        Logger.LogDebug("Saved band order to settings");
    }

    /// <summary>
    /// Gets active bands from a given DockSettings, considering this dock's monitor.
    /// </summary>
    private (ImmutableList<DockBandSettings> Start, ImmutableList<DockBandSettings> Center, ImmutableList<DockBandSettings> End) GetActiveBandsFromSettings(DockSettings dockSettings)
    {
        if (_monitorDeviceId is not null)
        {
            var config = FindMonitorConfig(dockSettings, _monitorDeviceId);
            if (config is not null)
            {
                return (
                    config.ResolveStartBands(dockSettings.StartBands),
                    config.ResolveCenterBands(dockSettings.CenterBands),
                    config.ResolveEndBands(dockSettings.EndBands));
            }
        }

        return (dockSettings.StartBands, dockSettings.CenterBands, dockSettings.EndBands);
    }

    private static Dictionary<string, DockBandSettings> BuildBandSettingsLookup(
        ImmutableList<DockBandSettings> start,
        ImmutableList<DockBandSettings> center,
        ImmutableList<DockBandSettings> end)
    {
        var lookup = new Dictionary<string, DockBandSettings>(StringComparer.Ordinal);
        foreach (var band in start.Concat(center).Concat(end))
        {
            lookup[band.CommandId] = band;
        }

        return lookup;
    }

    private static ImmutableList<DockBandSettings> MergeBandSettings(
        ImmutableList<DockBandSettings> targetBands,
        IReadOnlyDictionary<string, DockBandSettings> latestBandSettings,
        IReadOnlyDictionary<string, DockBandSettings> pendingBandSettings)
    {
        var merged = targetBands;
        for (var i = 0; i < merged.Count; i++)
        {
            var commandId = merged[i].CommandId;
            if (latestBandSettings.TryGetValue(commandId, out var settings)
                || pendingBandSettings.TryGetValue(commandId, out settings))
            {
                merged = merged.SetItem(i, settings);
            }
        }

        return merged;
    }

    private DockSettings? _snapshotDockSettings;
    private Dictionary<string, DockBandViewModel>? _snapshotBandViewModels;

    /// <summary>
    /// Takes a snapshot of the current band order and label settings before editing.
    /// Call this when entering edit mode.
    /// </summary>
    public void SnapshotBandOrder()
    {
        _isEditing = true;

        var dockSettings = _settingsService.Settings.DockSettings;

        var snapshotStartBandsCount = dockSettings.StartBands.Count;
        var snapshotCenterBandsCount = dockSettings.CenterBands.Count;
        var snapshotEndBandsCount = dockSettings.EndBands.Count;

        // Snapshot band ViewModels so we can restore unpinned bands
        // Use a dictionary but handle potential duplicates gracefully
        _snapshotDockSettings = dockSettings;
        _snapshotBandViewModels = new Dictionary<string, DockBandViewModel>();
        foreach (var band in StartItems.Concat(CenterItems).Concat(EndItems))
        {
            _snapshotBandViewModels.TryAdd(band.Id, band);
        }

        // Snapshot ShowLabels for all bands
        foreach (var band in _snapshotBandViewModels.Values)
        {
            band.SnapshotShowLabels();
        }

        Logger.LogDebug($"Snapshot taken: {snapshotStartBandsCount} start bands, {snapshotCenterBandsCount} center bands, {snapshotEndBandsCount} end bands");
    }

    /// <summary>
    /// Restores the band order and label settings from the snapshot taken when entering edit mode.
    /// Call this when discarding edit mode changes.
    /// </summary>
    public void RestoreBandOrder()
    {
        if (_snapshotDockSettings == null || _snapshotBandViewModels == null)
        {
            Logger.LogWarning("No snapshot to restore from");
            return;
        }

        // Restore ShowLabels for all snapshotted bands
        foreach (var band in _snapshotBandViewModels.Values)
        {
            band.RestoreShowLabels();
        }

        // Restore settings from snapshot (immutable = just assign back)
        _settings = _snapshotDockSettings;

        // Rebuild UI collections from restored settings using the snapshotted ViewModels
        RebuildUICollectionsFromSnapshot();

        _snapshotDockSettings = null;
        _snapshotBandViewModels = null;
        _isEditing = false;
        Logger.LogDebug("Restored band order from snapshot");
    }

    private void RebuildUICollectionsFromSnapshot()
    {
        if (_snapshotBandViewModels == null)
        {
            return;
        }

        var (activeStart, activeCenter, activeEnd) = GetActiveBands();

        StartItems.Clear();
        CenterItems.Clear();
        EndItems.Clear();

        foreach (var bandSettings in activeStart)
        {
            if (_snapshotBandViewModels.TryGetValue(bandSettings.CommandId, out var bandVM))
            {
                StartItems.Add(bandVM);
            }
        }

        foreach (var bandSettings in activeCenter)
        {
            if (_snapshotBandViewModels.TryGetValue(bandSettings.CommandId, out var bandVM))
            {
                CenterItems.Add(bandVM);
            }
        }

        foreach (var bandSettings in activeEnd)
        {
            if (_snapshotBandViewModels.TryGetValue(bandSettings.CommandId, out var bandVM))
            {
                EndItems.Add(bandVM);
            }
        }
    }

    private void RebuildUICollections()
    {
        var (activeStart, activeCenter, activeEnd) = GetActiveBands();

        // Create a lookup of all current band ViewModels
        var allBands = StartItems.Concat(CenterItems).Concat(EndItems).ToDictionary(b => b.Id);

        StartItems.Clear();
        CenterItems.Clear();
        EndItems.Clear();

        foreach (var bandSettings in activeStart)
        {
            if (allBands.TryGetValue(bandSettings.CommandId, out var bandVM))
            {
                StartItems.Add(bandVM);
            }
        }

        foreach (var bandSettings in activeCenter)
        {
            if (allBands.TryGetValue(bandSettings.CommandId, out var bandVM))
            {
                CenterItems.Add(bandVM);
            }
        }

        foreach (var bandSettings in activeEnd)
        {
            if (allBands.TryGetValue(bandSettings.CommandId, out var bandVM))
            {
                EndItems.Add(bandVM);
            }
        }
    }

    /// <summary>
    /// Gets the list of dock bands that are not currently pinned to any section.
    /// </summary>
    public IEnumerable<TopLevelViewModel> GetAvailableBandsToAdd()
    {
        // Get IDs of all bands currently in the dock
        var pinnedBandIds = new HashSet<string>();
        foreach (var band in StartItems)
        {
            pinnedBandIds.Add(band.Id);
        }

        foreach (var band in CenterItems)
        {
            pinnedBandIds.Add(band.Id);
        }

        foreach (var band in EndItems)
        {
            pinnedBandIds.Add(band.Id);
        }

        // Return all dock bands that are not already pinned
        return AllItems.Where(tlc => !pinnedBandIds.Contains(tlc.Id));
    }

    /// <summary>
    /// Adds a band to the specified dock section.
    /// Does not save to disk - call SaveBandOrder() when done editing.
    /// </summary>
    public void AddBandToSection(TopLevelViewModel topLevel, DockPinSide targetSide)
    {
        var bandId = topLevel.Id;

        // Check if already in the dock
        if (FindBandById(bandId) != null)
        {
            Logger.LogWarning($"Band {bandId} is already in the dock");
            return;
        }

        // Create settings for the new band
        var bandSettings = new DockBandSettings { ProviderId = topLevel.CommandProviderId, CommandId = bandId };
        var dockSettings = _settings;
        var (activeStart, activeCenter, activeEnd) = GetActiveBands();

        // Create the band view model
        var bandVm = CreateBandItem(bandSettings, topLevel.ItemViewModel);

        // Add to the appropriate section
        switch (targetSide)
        {
            case DockPinSide.Start:
                _settings = WithActiveBands(activeStart.Add(bandSettings), activeCenter, activeEnd);
                StartItems.Add(bandVm);
                break;
            case DockPinSide.Center:
                _settings = WithActiveBands(activeStart, activeCenter.Add(bandSettings), activeEnd);
                CenterItems.Add(bandVm);
                break;
            case DockPinSide.End:
                _settings = WithActiveBands(activeStart, activeCenter, activeEnd.Add(bandSettings));
                EndItems.Add(bandVm);
                break;
        }

        // Snapshot the new band so it can be removed on discard
        bandVm.SnapshotShowLabels();

        Task.Run(() =>
        {
            bandVm.SafeInitializePropertiesSynchronous();
        });

        Logger.LogDebug($"Added band {bandId} to {targetSide} (not saved yet)");
    }

    /// <summary>
    /// Unpins a band from the dock, removing it from whichever section it's in.
    /// Does not save to disk - call SaveBandOrder() when done editing.
    /// </summary>
    public void UnpinBand(DockBandViewModel band)
    {
        var bandId = band.Id;
        var (activeStart, activeCenter, activeEnd) = GetActiveBands();

        // Remove from settings
        _settings = WithActiveBands(
            activeStart.RemoveAll(b => b.CommandId == bandId),
            activeCenter.RemoveAll(b => b.CommandId == bandId),
            activeEnd.RemoveAll(b => b.CommandId == bandId));

        // Remove from UI collections
        StartItems.Remove(band);
        CenterItems.Remove(band);
        EndItems.Remove(band);

        Logger.LogDebug($"Unpinned band {bandId} (not saved yet)");
    }

    /// <summary>
    /// Removes a band from this dock by its ID. Used when a band is dragged to
    /// another monitor's dock. Does not save — save happens when exiting edit mode.
    /// </summary>
    public void RemoveBandById(string bandId)
    {
        if (FindBandById(bandId) == null)
        {
            return;
        }

        EnsureMonitorForked();

        var (activeStart, activeCenter, activeEnd) = GetActiveBands();

        _settings = WithActiveBands(
            activeStart.RemoveAll(b => b.CommandId == bandId),
            activeCenter.RemoveAll(b => b.CommandId == bandId),
            activeEnd.RemoveAll(b => b.CommandId == bandId));

        RemoveBandFromCollection(StartItems, bandId);
        RemoveBandFromCollection(CenterItems, bandId);
        RemoveBandFromCollection(EndItems, bandId);

        Logger.LogDebug($"Removed band {bandId} from monitor {_monitorDeviceId} (cross-monitor drag)");
    }

    /// <summary>
    /// Accepts a dock band from another monitor during a cross-monitor drag.
    /// Creates the band ViewModel and inserts it at the specified position.
    /// Does not save — save happens when exiting edit mode.
    /// </summary>
    public void AcceptBandFromMonitor(string bandId, DockPinSide targetSide, int targetIndex)
    {
        if (FindBandById(bandId) != null)
        {
            Logger.LogWarning($"AcceptBandFromMonitor: band {bandId} already in this dock");
            return;
        }

        EnsureMonitorForked();

        var topLevel = _topLevelCommandManager.LookupDockBand(bandId);
        if (topLevel is null)
        {
            Logger.LogWarning($"AcceptBandFromMonitor: band {bandId} not found in DockBandsSnapshot");
            return;
        }

        var bandSettings = new DockBandSettings { ProviderId = topLevel.CommandProviderId, CommandId = bandId };
        var bandVm = CreateBandItem(bandSettings, topLevel.ItemViewModel);

        var (activeStart, activeCenter, activeEnd) = GetActiveBands();

        switch (targetSide)
        {
            case DockPinSide.Start:
            {
                var idx = Math.Min(targetIndex, activeStart.Count);
                activeStart = activeStart.Insert(idx, bandSettings);
                var uiIdx = Math.Min(targetIndex, StartItems.Count);
                StartItems.Insert(uiIdx, bandVm);
                break;
            }

            case DockPinSide.Center:
            {
                var idx = Math.Min(targetIndex, activeCenter.Count);
                activeCenter = activeCenter.Insert(idx, bandSettings);
                var uiIdx = Math.Min(targetIndex, CenterItems.Count);
                CenterItems.Insert(uiIdx, bandVm);
                break;
            }

            case DockPinSide.End:
            {
                var idx = Math.Min(targetIndex, activeEnd.Count);
                activeEnd = activeEnd.Insert(idx, bandSettings);
                var uiIdx = Math.Min(targetIndex, EndItems.Count);
                EndItems.Insert(uiIdx, bandVm);
                break;
            }
        }

        _settings = WithActiveBands(activeStart, activeCenter, activeEnd);

        bandVm.SnapshotShowLabels();
        Task.Run(() =>
        {
            bandVm.SafeInitializePropertiesSynchronous();
        });

        Logger.LogDebug($"Accepted band {bandId} at {targetSide}[{targetIndex}] on monitor {_monitorDeviceId}");
    }

    private static void RemoveBandFromCollection(ObservableCollection<DockBandViewModel> collection, string bandId)
    {
        for (var i = collection.Count - 1; i >= 0; i--)
        {
            if (collection[i].Id == bandId)
            {
                collection.RemoveAt(i);
            }
        }
    }

    private void DoOnUiThread(Action action)
    {
        Task.Factory.StartNew(
            action,
            CancellationToken.None,
            TaskCreationOptions.None,
            Scheduler);
    }

    public CommandItemViewModel GetContextMenuForDock()
    {
        var model = new DockContextMenuItem();
        var vm = new CommandItemViewModel(new(model), new(_pageContext), contextMenuFactory: null);
        vm.SlowInitializeProperties();
        return vm;
    }

    private sealed partial class DockContextMenuItem : CommandItem
    {
        public DockContextMenuItem()
        {
            var editDockCommand = new AnonymousCommand(
                action: () =>
                {
                    WeakReferenceMessenger.Default.Send(new EnterDockEditModeMessage());
                })
            {
                Name = Properties.Resources.dock_edit_dock_name,
                Icon = Icons.EditIcon,
            };

            var openSettingsCommand = new AnonymousCommand(
                action: () =>
                {
                    WeakReferenceMessenger.Default.Send(new OpenSettingsMessage(SettingsPageTags.Dock));
                })
            {
                Name = Properties.Resources.dock_settings_name,
                Icon = Icons.SettingsIcon,
            };

            MoreCommands = new CommandContextItem[]
            {
                new CommandContextItem(editDockCommand),
                new CommandContextItem(openSettingsCommand),
            };
        }
    }

    private void EmitDockConfiguration()
    {
        var isDockEnabled = _settingsService.Settings.EnableDock;
        var dockSide = isDockEnabled ? GetEffectiveSide().ToString().ToLowerInvariant() : "none";
        /*
        // TODO: re-enable the collection of active bands at some point, if
        // we ever get data flowing again.

        var (activeStart, activeCenter, activeEnd) = GetActiveBands();

        static string FormatBands(ImmutableList<DockBandSettings> bands) =>
            string.Join("\n", bands.Select(b => $"{b.ProviderId}/{b.CommandId}"));

        var startBands = isDockEnabled ? FormatBands(activeStart) : string.Empty;
        var centerBands = isDockEnabled ? FormatBands(activeCenter) : string.Empty;
        var endBands = isDockEnabled ? FormatBands(activeEnd) : string.Empty;
        */
        var startBands = string.Empty;
        var centerBands = string.Empty;
        var endBands = string.Empty;
        WeakReferenceMessenger.Default.Send(new TelemetryDockConfigurationMessage(
            isDockEnabled, dockSide, startBands, centerBands, endBands));
    }

    /// <summary>
    /// Provides an empty page context, for the dock's own context menu. We're
    /// building the context menu for the dock using literally our own cmdpal
    /// types, but that means we need a page context for the VM we will
    /// generate.
    /// </summary>
    private sealed partial class DockPageContext(DockViewModel dockViewModel) : IPageContext
    {
        public TaskScheduler Scheduler => dockViewModel.Scheduler;

        public ICommandProviderContext ProviderContext => CommandProviderContext.Empty;

        public void ShowException(Exception ex, string? extensionHint)
        {
            var extensionText = extensionHint ?? "<unknown>";
            Logger.LogError($"Error in dock context {extensionText}", ex);
        }
    }
}
