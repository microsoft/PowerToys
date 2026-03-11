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

public sealed partial class DockViewModel : IDisposable
{
    private readonly TopLevelCommandManager _topLevelCommandManager;
    private readonly SettingsService _settingsService;
    private readonly DockPageContext _pageContext; // only to be used for our own context menu - not for dock bands themselves
    private readonly IContextMenuFactory _contextMenuFactory;
    private readonly string? _monitorDeviceId;

    private SettingsModel _settingsModel;
    private DockSettings _settings;
    private DockMonitorConfig? _monitorConfig;

    public TaskScheduler Scheduler { get; }

    public ObservableCollection<DockBandViewModel> StartItems { get; } = new();

    public ObservableCollection<DockBandViewModel> CenterItems { get; } = new();

    public ObservableCollection<DockBandViewModel> EndItems { get; } = new();

    public IReadOnlyList<TopLevelViewModel> AllItems => _topLevelCommandManager.GetDockBandsSnapshot();

    public DockViewModel(
        TopLevelCommandManager tlcManager,
        IContextMenuFactory contextMenuFactory,
        SettingsService settingsService,
        TaskScheduler scheduler,
        string? monitorDeviceId = null)
    {
        _topLevelCommandManager = tlcManager;
        _contextMenuFactory = contextMenuFactory;
        _settingsService = settingsService;
        _monitorDeviceId = monitorDeviceId;

        _settingsModel = _settingsService.CurrentSettings;
        _settings = _settingsModel.DockSettings;

        RefreshMonitorConfig();

        _settingsService.SettingsChanged += SettingsService_SettingsChanged;

        Scheduler = scheduler;
        _pageContext = new(this);

        _topLevelCommandManager.DockBands.CollectionChanged += DockBands_CollectionChanged;

        EmitDockConfiguration();
    }

    private void SettingsService_SettingsChanged(SettingsService sender, SettingsChangedEventArgs args)
    {
        _settingsModel = args.NewSettingsModel;
        _settings = _settingsModel.DockSettings;
        RefreshMonitorConfig();
    }

    /// <summary>
    /// Looks up the <see cref="DockMonitorConfig"/> for this VM's monitor from
    /// the current settings. Called on construction and whenever settings change
    /// so the reference stays fresh.
    /// </summary>
    private void RefreshMonitorConfig()
    {
        if (_monitorDeviceId is null)
        {
            _monitorConfig = null;
            return;
        }

        _monitorConfig = null;
        foreach (var config in _settings.MonitorConfigs)
        {
            if (string.Equals(config.MonitorDeviceId, _monitorDeviceId, StringComparison.OrdinalIgnoreCase))
            {
                _monitorConfig = config;
                break;
            }
        }
    }

    /// <summary>
    /// Returns the band lists this VM should read from and write to.
    /// When a per-monitor config is active and customized, returns the
    /// monitor's own lists; otherwise the global dock settings lists.
    /// </summary>
    private (List<DockBandSettings> Start, List<DockBandSettings> Center, List<DockBandSettings> End) GetActiveBandLists()
    {
        if (_monitorConfig is not null && _monitorConfig.IsCustomized
            && _monitorConfig.StartBands is not null
            && _monitorConfig.CenterBands is not null
            && _monitorConfig.EndBands is not null)
        {
            return (_monitorConfig.StartBands, _monitorConfig.CenterBands, _monitorConfig.EndBands);
        }

        return (_settings.StartBands, _settings.CenterBands, _settings.EndBands);
    }

    /// <summary>
    /// If this VM targets a specific monitor that hasn't been customized yet,
    /// forks the global bands into per-monitor lists so subsequent mutations
    /// only affect this monitor.
    /// </summary>
    private void EnsureMonitorForked()
    {
        if (_monitorConfig is not null && !_monitorConfig.IsCustomized)
        {
            _monitorConfig.ForkFromGlobal(_settings);
        }
    }

    /// <summary>
    /// Finds a <see cref="DockBandSettings"/> by command ID across the three
    /// supplied lists without LINQ.
    /// </summary>
    private static DockBandSettings? FindBandSettingsById(
        string bandId,
        List<DockBandSettings> start,
        List<DockBandSettings> center,
        List<DockBandSettings> end)
    {
        foreach (var b in start)
        {
            if (b.CommandId == bandId)
            {
                return b;
            }
        }

        foreach (var b in center)
        {
            if (b.CommandId == bandId)
            {
                return b;
            }
        }

        foreach (var b in end)
        {
            if (b.CommandId == bandId)
            {
                return b;
            }
        }

        return null;
    }

    private void DockBands_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        Logger.LogDebug("Starting DockBands_CollectionChanged");
        SetupBands();
        Logger.LogDebug("Ended DockBands_CollectionChanged");
    }

    public void UpdateSettings(DockSettings settings)
    {
        Logger.LogDebug($"DockViewModel.UpdateSettings");
        _settings = settings;
        SetupBands();
    }

    /// <summary>
    /// Performs the initial band setup. Call once after the DockWindow has been
    /// created and shown so the UI scheduler is available. The constructor
    /// intentionally skips this to avoid <see cref="System.ExecutionEngineException"/>
    /// in AOT builds where the scheduler isn't ready during construction.
    /// </summary>
    public void InitializeBands() => SetupBands();

    private void SetupBands()
    {
        Logger.LogDebug($"Setting up dock bands");
        var (startBands, centerBands, endBands) = GetActiveBandLists();
        SetupBands(startBands, StartItems);
        SetupBands(centerBands, CenterItems);
        SetupBands(endBands, EndItems);
    }

    private void SetupBands(
        List<DockBandSettings> bands,
        ObservableCollection<DockBandViewModel> target)
    {
        List<DockBandViewModel> newBands = new();
        foreach (var band in bands)
        {
            var commandId = band.CommandId;
            var topLevelCommand = _topLevelCommandManager.LookupDockBand(commandId);

            if (topLevelCommand is null)
            {
                Logger.LogWarning($"Failed to find band {commandId}");
            }

            if (topLevelCommand is not null)
            {
                // note: CreateBandItem doesn't actually initialize the band, it
                // just creates the VM. Callers need to make sure to call
                // InitializeProperties() on a BG thread elsewhere
                var bandVm = CreateBandItem(band, topLevelCommand.ItemViewModel);
                newBands.Add(bandVm);
            }
        }

        var beforeCount = target.Count;
        var afterCount = newBands.Count;

        DoOnUiThread(() =>
        {
            List<DockBandViewModel> removed = new();
            ListHelpers.InPlaceUpdateList(target, newBands, out removed);
            var isStartBand = target == StartItems;
            var label = isStartBand ? "Start bands:" : "End bands:";
            Logger.LogDebug($"{label} ({beforeCount}) -> ({afterCount}), Removed {removed?.Count ?? 0} items");

            // then, back to a BG thread:
            Task.Run(() =>
            {
                if (removed is not null)
                {
                    foreach (var removedItem in removed)
                    {
                        removedItem.SafeCleanup();
                    }
                }
            });
        });

        // Initialize properties on BG thread
        Task.Run(() =>
        {
            foreach (var band in newBands)
            {
                band.SafeInitializePropertiesSynchronous();
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
        DockBandViewModel band = new(commandItem, commandItem.PageContext, bandSettings, _settings, _contextMenuFactory);

        // the band is NOT initialized here!
        return band;
    }

    private void SaveSettings()
    {
        _settingsService.SaveSettings(_settingsModel);
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
        EnsureMonitorForked();
        var (startBands, centerBands, endBands) = GetActiveBandLists();

        var bandSettings = FindBandSettingsById(bandId, startBands, centerBands, endBands);

        if (bandSettings == null)
        {
            return;
        }

        // Remove from all settings lists
        startBands.RemoveAll(b => b.CommandId == bandId);
        centerBands.RemoveAll(b => b.CommandId == bandId);
        endBands.RemoveAll(b => b.CommandId == bandId);

        // Add to target settings list at the correct index
        var targetSettings = targetSide switch
        {
            DockPinSide.Start => startBands,
            DockPinSide.Center => centerBands,
            DockPinSide.End => endBands,
            _ => startBands,
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
        EnsureMonitorForked();
        var (startBands, centerBands, endBands) = GetActiveBandLists();

        var bandSettings = FindBandSettingsById(bandId, startBands, centerBands, endBands);

        if (bandSettings == null)
        {
            Logger.LogWarning($"Could not find band settings for band {bandId}");
            return;
        }

        // Remove from all sides (settings and UI)
        startBands.RemoveAll(b => b.CommandId == bandId);
        centerBands.RemoveAll(b => b.CommandId == bandId);
        endBands.RemoveAll(b => b.CommandId == bandId);
        StartItems.Remove(band);
        CenterItems.Remove(band);
        EndItems.Remove(band);

        // Add to the target side at the specified index
        switch (targetSide)
        {
            case DockPinSide.Start:
                {
                    var settingsIndex = Math.Min(targetIndex, startBands.Count);
                    startBands.Insert(settingsIndex, bandSettings);

                    var uiIndex = Math.Min(targetIndex, StartItems.Count);
                    StartItems.Insert(uiIndex, band);
                    break;
                }

            case DockPinSide.Center:
                {
                    var settingsIndex = Math.Min(targetIndex, centerBands.Count);
                    centerBands.Insert(settingsIndex, bandSettings);

                    var uiIndex = Math.Min(targetIndex, CenterItems.Count);
                    CenterItems.Insert(uiIndex, band);
                    break;
                }

            case DockPinSide.End:
                {
                    var settingsIndex = Math.Min(targetIndex, endBands.Count);
                    endBands.Insert(settingsIndex, bandSettings);

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
        // Save ShowLabels for all bands
        foreach (var band in StartItems)
        {
            band.SaveShowLabels();
        }

        foreach (var band in CenterItems)
        {
            band.SaveShowLabels();
        }

        foreach (var band in EndItems)
        {
            band.SaveShowLabels();
        }

        _snapshotStartBands = null;
        _snapshotCenterBands = null;
        _snapshotEndBands = null;
        _snapshotBandViewModels = null;
        _settingsService.SaveSettings(_settingsModel);
        Logger.LogDebug("Saved band order to settings");
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
        var (startBands, centerBands, endBands) = GetActiveBandLists();

        _snapshotStartBands = new List<DockBandSettings>(startBands.Count);
        foreach (var b in startBands)
        {
            _snapshotStartBands.Add(b with { });
        }

        _snapshotCenterBands = new List<DockBandSettings>(centerBands.Count);
        foreach (var b in centerBands)
        {
            _snapshotCenterBands.Add(b with { });
        }

        _snapshotEndBands = new List<DockBandSettings>(endBands.Count);
        foreach (var b in endBands)
        {
            _snapshotEndBands.Add(b with { });
        }

        // Snapshot band ViewModels so we can restore unpinned bands
        // Use a dictionary but handle potential duplicates gracefully
        _snapshotBandViewModels = new Dictionary<string, DockBandViewModel>();
        foreach (var band in StartItems)
        {
            _snapshotBandViewModels.TryAdd(band.Id, band);
        }

        foreach (var band in CenterItems)
        {
            _snapshotBandViewModels.TryAdd(band.Id, band);
        }

        foreach (var band in EndItems)
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

        var (startBands, centerBands, endBands) = GetActiveBandLists();

        // Restore settings from snapshot
        startBands.Clear();
        centerBands.Clear();
        endBands.Clear();

        foreach (var bandSnapshot in _snapshotStartBands)
        {
            var bandSettings = bandSnapshot with { };
            startBands.Add(bandSettings);
        }

        foreach (var bandSnapshot in _snapshotCenterBands)
        {
            var bandSettings = bandSnapshot with { };
            centerBands.Add(bandSettings);
        }

        foreach (var bandSnapshot in _snapshotEndBands)
        {
            var bandSettings = bandSnapshot with { };
            endBands.Add(bandSettings);
        }

        // Rebuild UI collections from restored settings using the snapshotted ViewModels
        RebuildUICollectionsFromSnapshot();

        _snapshotStartBands = null;
        _snapshotCenterBands = null;
        _snapshotEndBands = null;
        _snapshotBandViewModels = null;
        Logger.LogDebug("Restored band order from snapshot");
    }

    private void RebuildUICollectionsFromSnapshot()
    {
        if (_snapshotBandViewModels == null)
        {
            return;
        }

        var (startBands, centerBands, endBands) = GetActiveBandLists();

        StartItems.Clear();
        CenterItems.Clear();
        EndItems.Clear();

        foreach (var bandSettings in startBands)
        {
            if (_snapshotBandViewModels.TryGetValue(bandSettings.CommandId, out var bandVM))
            {
                StartItems.Add(bandVM);
            }
        }

        foreach (var bandSettings in centerBands)
        {
            if (_snapshotBandViewModels.TryGetValue(bandSettings.CommandId, out var bandVM))
            {
                CenterItems.Add(bandVM);
            }
        }

        foreach (var bandSettings in endBands)
        {
            if (_snapshotBandViewModels.TryGetValue(bandSettings.CommandId, out var bandVM))
            {
                EndItems.Add(bandVM);
            }
        }
    }

    private void RebuildUICollections()
    {
        var (startBands, centerBands, endBands) = GetActiveBandLists();

        // Create a lookup of all current band ViewModels
        var allBands = new Dictionary<string, DockBandViewModel>();
        foreach (var band in StartItems)
        {
            allBands.TryAdd(band.Id, band);
        }

        foreach (var band in CenterItems)
        {
            allBands.TryAdd(band.Id, band);
        }

        foreach (var band in EndItems)
        {
            allBands.TryAdd(band.Id, band);
        }

        StartItems.Clear();
        CenterItems.Clear();
        EndItems.Clear();

        foreach (var bandSettings in startBands)
        {
            if (allBands.TryGetValue(bandSettings.CommandId, out var bandVM))
            {
                StartItems.Add(bandVM);
            }
        }

        foreach (var bandSettings in centerBands)
        {
            if (allBands.TryGetValue(bandSettings.CommandId, out var bandVM))
            {
                CenterItems.Add(bandVM);
            }
        }

        foreach (var bandSettings in endBands)
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
    public List<TopLevelViewModel> GetAvailableBandsToAdd()
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
        var result = new List<TopLevelViewModel>();
        foreach (var tlc in AllItems)
        {
            if (!pinnedBandIds.Contains(tlc.Id))
            {
                result.Add(tlc);
            }
        }

        return result;
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

        EnsureMonitorForked();
        var (startBands, centerBands, endBands) = GetActiveBandLists();

        // Create settings for the new band
        var bandSettings = new DockBandSettings { ProviderId = topLevel.CommandProviderId, CommandId = bandId };

        // Create the band view model
        var bandVm = CreateBandItem(bandSettings, topLevel.ItemViewModel);

        // Add to the appropriate section
        switch (targetSide)
        {
            case DockPinSide.Start:
                startBands.Add(bandSettings);
                StartItems.Add(bandVm);
                break;
            case DockPinSide.Center:
                centerBands.Add(bandSettings);
                CenterItems.Add(bandVm);
                break;
            case DockPinSide.End:
                endBands.Add(bandSettings);
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
        EnsureMonitorForked();
        var (startBands, centerBands, endBands) = GetActiveBandLists();

        // Remove from settings
        startBands.RemoveAll(b => b.CommandId == bandId);
        centerBands.RemoveAll(b => b.CommandId == bandId);
        endBands.RemoveAll(b => b.CommandId == bandId);

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
        var isDockEnabled = _settingsModel.EnableDock;
        var dockSide = isDockEnabled ? _settings.Side.ToString().ToLowerInvariant() : "none";

        static string FormatBands(List<DockBandSettings> bands)
        {
            if (bands.Count == 0)
            {
                return string.Empty;
            }

            var parts = new string[bands.Count];
            for (var i = 0; i < bands.Count; i++)
            {
                parts[i] = $"{bands[i].ProviderId}/{bands[i].CommandId}";
            }

            return string.Join("\n", parts);
        }

        var (activeBandStart, activeBandCenter, activeBandEnd) = GetActiveBandLists();
        var startBands = isDockEnabled ? FormatBands(activeBandStart) : string.Empty;
        var centerBands = isDockEnabled ? FormatBands(activeBandCenter) : string.Empty;
        var endBands = isDockEnabled ? FormatBands(activeBandEnd) : string.Empty;

        WeakReferenceMessenger.Default.Send(new TelemetryDockConfigurationMessage(
            isDockEnabled, dockSide, startBands, centerBands, endBands));
    }

    public void Dispose()
    {
        _settingsService.SettingsChanged -= SettingsService_SettingsChanged;
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
