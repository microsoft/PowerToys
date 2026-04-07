// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.Dock;

public sealed partial class DockViewModel
{
    private readonly TopLevelCommandManager _topLevelCommandManager;
    private readonly ISettingsService _settingsService;
    private readonly DockPageContext _pageContext; // only to be used for our own context menu - not for dock bands themselves
    private readonly IContextMenuFactory _contextMenuFactory;

    // Lock ordering: TopLevelCommandManager._dockBandsLock must always be
    // acquired BEFORE this lock. Never call LookupDockBand or
    // GetDockBandsSnapshot while holding this lock.
    private readonly Lock _setupBandsLock = new();

    private long _dockBandsChangeVersion;
    private long _setupBandsRequestVersion;

    private DockSettings _settings;
    private volatile bool _isEditing;

    public TaskScheduler Scheduler { get; }

    public ObservableCollection<DockBandViewModel> StartItems { get; } = new();

    public ObservableCollection<DockBandViewModel> CenterItems { get; } = new();

    public ObservableCollection<DockBandViewModel> EndItems { get; } = new();

    public IReadOnlyList<TopLevelViewModel> AllItems => _topLevelCommandManager.GetDockBandsSnapshot();

    public DockViewModel(
        TopLevelCommandManager tlcManager,
        IContextMenuFactory contextMenuFactory,
        TaskScheduler scheduler,
        ISettingsService settingsService)
    {
        _topLevelCommandManager = tlcManager;
        _contextMenuFactory = contextMenuFactory;
        _settingsService = settingsService;
        _settings = _settingsService.Settings.DockSettings;
        Scheduler = scheduler;
        _pageContext = new(this);

        _topLevelCommandManager.DockBands.CollectionChanged += DockBands_CollectionChanged;

        EmitDockConfiguration();
    }

    private void DockBands_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        // Mark any snapshots taken before this event as stale
        Interlocked.Increment(ref _dockBandsChangeVersion);

        // Build the lookup while we're still inside _dockBandsLock (held by
        // TopLevelCommandManager). Reading DockBands directly is safe here
        // because the CollectionChanged event fires within that lock
        var lookup = BuildBandLookup(_topLevelCommandManager.DockBands);

        lock (_setupBandsLock)
        {
            if (_isEditing)
            {
                Logger.LogDebug("Skipping DockBands_CollectionChanged during edit mode");
                return;
            }

            Logger.LogDebug("Starting DockBands_CollectionChanged");
            SetupBands(lookup, ++_setupBandsRequestVersion);
            Logger.LogDebug("Ended DockBands_CollectionChanged");
        }
    }

    public void UpdateSettings(DockSettings settings)
    {
        Logger.LogDebug($"DockViewModel.UpdateSettings");

        while (true)
        {
            // acquire the snapshot before _setupBandsLock to respect lock ordering
            var observedDockBandsChangeVersion = Volatile.Read(ref _dockBandsChangeVersion);
            var lookup = BuildBandLookup(_topLevelCommandManager.GetDockBandsSnapshot());
            var shouldRetry = false;

            lock (_setupBandsLock)
            {
                _settings = settings;

                if (observedDockBandsChangeVersion != Volatile.Read(ref _dockBandsChangeVersion))
                {
                    shouldRetry = true;
                }
                else
                {
                    SetupBands(lookup, ++_setupBandsRequestVersion);
                }
            }

            if (!shouldRetry)
            {
                return;
            }

            Logger.LogDebug("Retrying DockViewModel.UpdateSettings due to concurrent DockBands change");
        }
    }

    private void SetupBands(Dictionary<string, TopLevelViewModel> lookup, long setupRequestVersion)
    {
        Logger.LogDebug($"Setting up dock bands");
        var addedBands = new HashSet<DockBandIdentity>();
        SetupBands(_settings.StartBands, StartItems, addedBands, lookup, setupRequestVersion);
        SetupBands(_settings.CenterBands, CenterItems, addedBands, lookup, setupRequestVersion);
        SetupBands(_settings.EndBands, EndItems, addedBands, lookup, setupRequestVersion);
    }

    private static Dictionary<string, TopLevelViewModel> BuildBandLookup(IEnumerable<TopLevelViewModel> dockBands)
    {
        var lookup = new Dictionary<string, TopLevelViewModel>();
        foreach (var band in dockBands)
        {
            lookup.TryAdd(band.Id, band);
        }

        return lookup;
    }

    private record DockBandIdentity(string ProviderId, string CommandId);

    private void SetupBands(
        List<DockBandSettings> requestedBands,
        ObservableCollection<DockBandViewModel> target,
        HashSet<DockBandIdentity> addedBands,
        Dictionary<string, TopLevelViewModel> lookup,
        long setupRequestVersion)
    {
        List<DockBandViewModel> newBands = [];
        List<DockBandViewModel> bandsNeedingInit = [];

        // Build a lookup of existing bands in this target for O(1) reuse checks.
        var existingByCommandId = new Dictionary<string, DockBandViewModel>();
        foreach (var existing in target)
        {
            existingByCommandId.TryAdd(existing.Id, existing);
        }

        // Deduplicate requestedBands across all sections:
        var deduplicatedRequestedBands = new List<DockBandSettings>();
        foreach (var requestedBand in requestedBands)
        {
            var identity = new DockBandIdentity(requestedBand.ProviderId, requestedBand.CommandId);
            if (addedBands.Add(identity))
            {
                deduplicatedRequestedBands.Add(requestedBand);
            }
        }

        // Now we have requested bands that we know are unique so far:
        foreach (var band in deduplicatedRequestedBands)
        {
            var commandId = band.CommandId;

            if (string.IsNullOrEmpty(commandId))
            {
                Logger.LogError($"Skipping band with empty command ID (provider: {band.ProviderId})");
                continue;
            }

            if (!lookup.TryGetValue(commandId, out var topLevelCommand))
            {
                Logger.LogWarning($"Failed to find band {commandId}");
                continue;
            }

            // Reuse the existing DockBandViewModel if:
            // 1. We already have one with this ID in the target, AND
            // 2. Its RootItem is the same reference as the current TopLevelViewModel's
            //    ItemViewModel (meaning the extension hasn't been reloaded and the
            //    COM objects are still alive)
            if (existingByCommandId.TryGetValue(commandId, out var existingBand) &&
                ReferenceEquals(existingBand.RootItem, topLevelCommand.ItemViewModel))
            {
                existingBand.RefreshSettings(band, _settings);
                newBands.Add(existingBand);
            }
            else
            {
                // Create a new band (extension reloaded, or band is new).
                // CreateBandItem doesn't initialize the band - callers need to
                // call InitializeProperties() on a BG thread elsewhere.
                var bandVm = CreateBandItem(band, topLevelCommand.ItemViewModel);
                newBands.Add(bandVm);
                bandsNeedingInit.Add(bandVm);
            }
        }

        var beforeCount = target.Count;
        var afterCount = newBands.Count;
        var label = target == StartItems ? "Start bands" : target == CenterItems ? "Center bands" : "End bands";

        DoOnUiThread(() =>
        {
            if (setupRequestVersion != Volatile.Read(ref _setupBandsRequestVersion))
            {
                Logger.LogDebug($"Skipping stale {label} update");
                CleanupBandsAsync(bandsNeedingInit);
                return;
            }

            List<DockBandViewModel> removed = new();
            ListHelpers.InPlaceUpdateList(target, newBands, out removed);
            Logger.LogDebug($"{label}: ({beforeCount}) -> ({afterCount}), Removed {removed.Count} items");

            // Cleanup removed items on a BG thread:
            CleanupBandsAsync(removed);

            // Initialize only newly created bands after they were successfully applied to the UI.
            if (bandsNeedingInit.Count > 0)
            {
                Task.Run(() =>
                {
                    foreach (var band in bandsNeedingInit)
                    {
                        band.SafeInitializePropertiesSynchronous();
                    }
                });
            }
        });
    }

    private static void CleanupBandsAsync(IReadOnlyCollection<DockBandViewModel> bands)
    {
        if (bands.Count == 0)
        {
            return;
        }

        Task.Run(() =>
        {
            foreach (var band in bands)
            {
                band.SafeCleanup();
            }
        });
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
        DockBandViewModel band = new(commandItem, commandItem.PageContext, bandSettings, _settings, SaveSettings, _contextMenuFactory);

        // the band is NOT initialized here!
        return band;
    }

    private void SaveSettings()
    {
        _settingsService.Save();
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
        var dockSettings = _settingsService.Settings.DockSettings;

        var bandSettings = dockSettings.StartBands.FirstOrDefault(b => b.CommandId == bandId)
                        ?? dockSettings.CenterBands.FirstOrDefault(b => b.CommandId == bandId)
                        ?? dockSettings.EndBands.FirstOrDefault(b => b.CommandId == bandId);

        if (bandSettings == null)
        {
            return;
        }

        // Remove from all settings lists
        dockSettings.StartBands.RemoveAll(b => b.CommandId == bandId);
        dockSettings.CenterBands.RemoveAll(b => b.CommandId == bandId);
        dockSettings.EndBands.RemoveAll(b => b.CommandId == bandId);

        // Add to target settings list at the correct index
        var targetSettings = targetSide switch
        {
            DockPinSide.Start => dockSettings.StartBands,
            DockPinSide.Center => dockSettings.CenterBands,
            DockPinSide.End => dockSettings.EndBands,
            _ => dockSettings.StartBands,
        };
        var insertIndex = Math.Min(targetIndex, targetSettings.Count);
        targetSettings.Insert(insertIndex, bandSettings);
    }

    /// <summary>
    /// Moves a dock band to a new position (cross-list drop).
    /// Does not save to disk - call SaveBandOrder() when done editing.
    /// </summary>
    public void MoveBandWithoutSaving(DockBandViewModel band, DockPinSide targetSide, int targetIndex)
    {
        var bandId = band.Id;
        var dockSettings = _settingsService.Settings.DockSettings;

        var bandSettings = dockSettings.StartBands.FirstOrDefault(b => b.CommandId == bandId)
                        ?? dockSettings.CenterBands.FirstOrDefault(b => b.CommandId == bandId)
                        ?? dockSettings.EndBands.FirstOrDefault(b => b.CommandId == bandId);

        if (bandSettings == null)
        {
            Logger.LogWarning($"Could not find band settings for band {bandId}");
            return;
        }

        // Remove from all sides (settings and UI)
        dockSettings.StartBands.RemoveAll(b => b.CommandId == bandId);
        dockSettings.CenterBands.RemoveAll(b => b.CommandId == bandId);
        dockSettings.EndBands.RemoveAll(b => b.CommandId == bandId);
        StartItems.Remove(band);
        CenterItems.Remove(band);
        EndItems.Remove(band);

        // Add to the target side at the specified index
        switch (targetSide)
        {
            case DockPinSide.Start:
                {
                    var settingsIndex = Math.Min(targetIndex, dockSettings.StartBands.Count);
                    dockSettings.StartBands.Insert(settingsIndex, bandSettings);

                    var uiIndex = Math.Min(targetIndex, StartItems.Count);
                    StartItems.Insert(uiIndex, band);
                    break;
                }

            case DockPinSide.Center:
                {
                    var settingsIndex = Math.Min(targetIndex, dockSettings.CenterBands.Count);
                    dockSettings.CenterBands.Insert(settingsIndex, bandSettings);

                    var uiIndex = Math.Min(targetIndex, CenterItems.Count);
                    CenterItems.Insert(uiIndex, band);
                    break;
                }

            case DockPinSide.End:
                {
                    var settingsIndex = Math.Min(targetIndex, dockSettings.EndBands.Count);
                    dockSettings.EndBands.Insert(settingsIndex, bandSettings);

                    var uiIndex = Math.Min(targetIndex, EndItems.Count);
                    EndItems.Insert(uiIndex, band);
                    break;
                }
        }

        Logger.LogDebug($"Moved band {bandId} to {targetSide} at index {targetIndex} (not saved yet)");
    }

    /// <summary>
    /// Saves the current band order and label settings to settings.
    /// Call this when exiting edit mode.
    /// </summary>
    public void SaveBandOrder()
    {
        SyncSettingsToCurrentUiOrder();

        _snapshotStartBands = null;
        _snapshotCenterBands = null;
        _snapshotEndBands = null;
        _snapshotBandViewModels = null;

        // Save without hotReload to avoid triggering SettingsChanged → SetupBands,
        // which could race with stale DockBands_CollectionChanged work items and
        // re-add bands that were just unpinned.
        _settingsService.Save(hotReload: false);
        _isEditing = false;
        Logger.LogDebug("Saved band order to settings");
    }

    private void SyncSettingsToCurrentUiOrder()
    {
        var dockSettings = _settingsService.Settings.DockSettings;
        var settingsByIdentity = dockSettings.StartBands
            .Concat(dockSettings.CenterBands)
            .Concat(dockSettings.EndBands)
            .GroupBy(b => new DockBandIdentity(b.ProviderId, b.CommandId))
            .ToDictionary(group => group.Key, group => group.First());

        var orderedStartBands = BuildOrderedBandSettings(StartItems, settingsByIdentity);
        var orderedCenterBands = BuildOrderedBandSettings(CenterItems, settingsByIdentity);
        var orderedEndBands = BuildOrderedBandSettings(EndItems, settingsByIdentity);

        dockSettings.StartBands.Clear();
        dockSettings.CenterBands.Clear();
        dockSettings.EndBands.Clear();

        dockSettings.StartBands.AddRange(orderedStartBands);
        dockSettings.CenterBands.AddRange(orderedCenterBands);
        dockSettings.EndBands.AddRange(orderedEndBands);
    }

    private List<DockBandSettings> BuildOrderedBandSettings(
        IEnumerable<DockBandViewModel> bands,
        Dictionary<DockBandIdentity, DockBandSettings> settingsByIdentity)
    {
        List<DockBandSettings> orderedBands = new();

        foreach (var band in bands)
        {
            var identity = new DockBandIdentity(band.ProviderId, band.Id);
            if (!settingsByIdentity.TryGetValue(identity, out var bandSettings))
            {
                Logger.LogWarning($"Missing settings for band {band.ProviderId}/{band.Id}; creating a replacement entry");
                bandSettings = new DockBandSettings
                {
                    ProviderId = band.ProviderId,
                    CommandId = band.Id,
                };
                settingsByIdentity[identity] = bandSettings;
            }

            band.SaveShowLabels(bandSettings);
            orderedBands.Add(bandSettings);
        }

        return orderedBands;
    }

    private List<DockBandSettings>? _snapshotStartBands;
    private List<DockBandSettings>? _snapshotCenterBands;
    private List<DockBandSettings>? _snapshotEndBands;
    private Dictionary<string, DockBandViewModel>? _snapshotBandViewModels;

    /// <summary>
    /// Takes a snapshot of the current band order and label settings before editing.
    /// Call this when entering edit mode.
    /// </summary>
    public void SnapshotBandOrder()
    {
        _isEditing = true;

        var dockSettings = _settingsService.Settings.DockSettings;
        _snapshotStartBands = dockSettings.StartBands.Select(b => b.Clone()).ToList();
        _snapshotCenterBands = dockSettings.CenterBands.Select(b => b.Clone()).ToList();
        _snapshotEndBands = dockSettings.EndBands.Select(b => b.Clone()).ToList();

        // Snapshot band ViewModels so we can restore unpinned bands
        // Use a dictionary but handle potential duplicates gracefully
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

        Logger.LogDebug($"Snapshot taken: {_snapshotStartBands.Count} start bands, {_snapshotCenterBands.Count} center bands, {_snapshotEndBands.Count} end bands");
    }

    /// <summary>
    /// Restores the band order and label settings from the snapshot taken when entering edit mode.
    /// Call this when discarding edit mode changes.
    /// </summary>
    public void RestoreBandOrder()
    {
        if (_snapshotStartBands == null ||
            _snapshotCenterBands == null ||
            _snapshotEndBands == null || _snapshotBandViewModels == null)
        {
            Logger.LogWarning("No snapshot to restore from");
            return;
        }

        // Restore ShowLabels for all snapshotted bands
        foreach (var band in _snapshotBandViewModels.Values)
        {
            band.RestoreShowLabels();
        }

        var dockSettings = _settingsService.Settings.DockSettings;

        // Restore settings from snapshot
        dockSettings.StartBands.Clear();
        dockSettings.CenterBands.Clear();
        dockSettings.EndBands.Clear();

        foreach (var bandSnapshot in _snapshotStartBands)
        {
            var bandSettings = bandSnapshot.Clone();
            dockSettings.StartBands.Add(bandSettings);
        }

        foreach (var bandSnapshot in _snapshotCenterBands)
        {
            var bandSettings = bandSnapshot.Clone();
            dockSettings.CenterBands.Add(bandSettings);
        }

        foreach (var bandSnapshot in _snapshotEndBands)
        {
            var bandSettings = bandSnapshot.Clone();
            dockSettings.EndBands.Add(bandSettings);
        }

        // Rebuild UI collections from restored settings using the snapshotted ViewModels
        RebuildUICollectionsFromSnapshot();

        _snapshotStartBands = null;
        _snapshotCenterBands = null;
        _snapshotEndBands = null;
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

        var dockSettings = _settingsService.Settings.DockSettings;

        StartItems.Clear();
        CenterItems.Clear();
        EndItems.Clear();

        foreach (var bandSettings in dockSettings.StartBands)
        {
            if (_snapshotBandViewModels.TryGetValue(bandSettings.CommandId, out var bandVM))
            {
                StartItems.Add(bandVM);
            }
        }

        foreach (var bandSettings in dockSettings.CenterBands)
        {
            if (_snapshotBandViewModels.TryGetValue(bandSettings.CommandId, out var bandVM))
            {
                CenterItems.Add(bandVM);
            }
        }

        foreach (var bandSettings in dockSettings.EndBands)
        {
            if (_snapshotBandViewModels.TryGetValue(bandSettings.CommandId, out var bandVM))
            {
                EndItems.Add(bandVM);
            }
        }
    }

    private void RebuildUICollections()
    {
        var dockSettings = _settingsService.Settings.DockSettings;

        // Create a lookup of all current band ViewModels
        var allBands = StartItems.Concat(CenterItems).Concat(EndItems).ToDictionary(b => b.Id);

        StartItems.Clear();
        CenterItems.Clear();
        EndItems.Clear();

        foreach (var bandSettings in dockSettings.StartBands)
        {
            if (allBands.TryGetValue(bandSettings.CommandId, out var bandVM))
            {
                StartItems.Add(bandVM);
            }
        }

        foreach (var bandSettings in dockSettings.CenterBands)
        {
            if (allBands.TryGetValue(bandSettings.CommandId, out var bandVM))
            {
                CenterItems.Add(bandVM);
            }
        }

        foreach (var bandSettings in dockSettings.EndBands)
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

        if (string.IsNullOrEmpty(bandId))
        {
            Logger.LogError($"Cannot add band with empty ID from provider {topLevel.CommandProviderId}");
            return;
        }

        // Check if already in the dock
        if (FindBandById(bandId) != null)
        {
            Logger.LogWarning($"Band {bandId} is already in the dock");
            return;
        }

        // Create settings for the new band
        var bandSettings = new DockBandSettings { ProviderId = topLevel.CommandProviderId, CommandId = bandId, ShowLabels = null };
        var dockSettings = _settingsService.Settings.DockSettings;

        // Create the band view model
        var bandVm = CreateBandItem(bandSettings, topLevel.ItemViewModel);

        // Add to the appropriate section
        switch (targetSide)
        {
            case DockPinSide.Start:
                dockSettings.StartBands.Add(bandSettings);
                StartItems.Add(bandVm);
                break;
            case DockPinSide.Center:
                dockSettings.CenterBands.Add(bandSettings);
                CenterItems.Add(bandVm);
                break;
            case DockPinSide.End:
                dockSettings.EndBands.Add(bandSettings);
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
        var dockSettings = _settingsService.Settings.DockSettings;

        // Remove from settings
        dockSettings.StartBands.RemoveAll(b => b.CommandId == bandId);
        dockSettings.CenterBands.RemoveAll(b => b.CommandId == bandId);
        dockSettings.EndBands.RemoveAll(b => b.CommandId == bandId);

        // Remove from UI collections
        StartItems.Remove(band);
        CenterItems.Remove(band);
        EndItems.Remove(band);

        Logger.LogDebug($"Unpinned band {bandId} (not saved yet)");
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
                    WeakReferenceMessenger.Default.Send(new OpenSettingsMessage("Dock"));
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
        var dockSide = isDockEnabled ? _settings.Side.ToString().ToLowerInvariant() : "none";

        static string FormatBands(List<DockBandSettings> bands) =>
            string.Join("\n", bands.Select(b => $"{b.ProviderId}/{b.CommandId}"));

        var startBands = isDockEnabled ? FormatBands(_settings.StartBands) : string.Empty;
        var centerBands = isDockEnabled ? FormatBands(_settings.CenterBands) : string.Empty;
        var endBands = isDockEnabled ? FormatBands(_settings.EndBands) : string.Empty;

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
