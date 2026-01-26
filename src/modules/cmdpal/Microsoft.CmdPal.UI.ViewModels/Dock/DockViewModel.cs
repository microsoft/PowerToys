// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.Core.Common;
using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.Dock;

public sealed partial class DockViewModel : IDisposable,
    IRecipient<CommandsReloadedMessage>,
    IPageContext
{
    private readonly TopLevelCommandManager _topLevelCommandManager;
    private readonly SettingsModel _settingsModel;

    private DockSettings _settings;

    // private DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    // private DispatcherQueue _updateWindowsQueue = DispatcherQueueController.CreateOnDedicatedThread().DispatcherQueue;
    public TaskScheduler Scheduler { get; }

    public ObservableCollection<DockBandViewModel> StartItems { get; } = new();

    public ObservableCollection<DockBandViewModel> CenterItems { get; } = new();

    public ObservableCollection<DockBandViewModel> EndItems { get; } = new();

    public ObservableCollection<TopLevelViewModel> AllItems => _topLevelCommandManager.DockBands;

    public DockViewModel(
        TopLevelCommandManager tlcManager,
        SettingsModel settings,
        TaskScheduler scheduler)
    {
        _topLevelCommandManager = tlcManager;
        _settingsModel = settings;
        _settings = settings.DockSettings;
        Scheduler = scheduler;
        WeakReferenceMessenger.Default.Register<CommandsReloadedMessage>(this);
    }

    public void UpdateSettings(DockSettings settings)
    {
        Logger.LogDebug($"DockViewModel.UpdateSettings");
        _settings = settings;
        SetupBands();
    }

    private void SetupBands()
    {
        Logger.LogDebug($"Setting up dock bands");
        SetupBands(_settings.StartBands, StartItems);
        SetupBands(_settings.CenterBands, CenterItems);
        SetupBands(_settings.EndBands, EndItems);
    }

    private void SetupBands(
        List<DockBandSettings> bands,
        ObservableCollection<DockBandViewModel> target)
    {
        List<DockBandViewModel> newBands = new();
        foreach (var band in bands)
        {
            var commandId = band.Id;
            var topLevelCommand = _topLevelCommandManager.LookupDockBand(commandId);

            if (topLevelCommand is null)
            {
                Logger.LogWarning($"Failed to find band {commandId}");
            }

            if (topLevelCommand is not null)
            {
                var bandVm = CreateBandItem(band, topLevelCommand.ItemViewModel);
                newBands.Add(bandVm);
            }
        }

        var beforeCount = target.Count;
        var afterCount = newBands.Count;

        DoOnUiThread(() =>
        {
            ListHelpers.InPlaceUpdateList(target, newBands, out var removed);
            var isStartBand = target == StartItems;
            var label = isStartBand ? "Start bands:" : "End bands:";
            Logger.LogDebug($"{label} ({beforeCount}) -> ({afterCount}), Removed {removed?.Count ?? 0} items");
        });
    }

    public void Dispose()
    {
    }

    public void Receive(CommandsReloadedMessage message)
    {
        SetupBands();
        CoreLogger.LogDebug("Bands reloaded");
    }

    private DockBandViewModel CreateBandItem(
        DockBandSettings bandSettings,
        CommandItemViewModel commandItem)
    {
        DockBandViewModel band = new(commandItem, new(this), bandSettings, _settings, SaveSettings);
        band.InitializeProperties(); // TODO! make async
        return band;
    }

    private void SaveSettings()
    {
        SettingsModel.SaveSettings(_settingsModel);
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
        var dockSettings = _settingsModel.DockSettings;

        var bandSettings = dockSettings.StartBands.FirstOrDefault(b => b.Id == bandId)
                        ?? dockSettings.CenterBands.FirstOrDefault(b => b.Id == bandId)
                        ?? dockSettings.EndBands.FirstOrDefault(b => b.Id == bandId);

        if (bandSettings == null)
        {
            return;
        }

        // Remove from all settings lists
        dockSettings.StartBands.RemoveAll(b => b.Id == bandId);
        dockSettings.CenterBands.RemoveAll(b => b.Id == bandId);
        dockSettings.EndBands.RemoveAll(b => b.Id == bandId);

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
        var dockSettings = _settingsModel.DockSettings;

        var bandSettings = dockSettings.StartBands.FirstOrDefault(b => b.Id == bandId)
                        ?? dockSettings.CenterBands.FirstOrDefault(b => b.Id == bandId)
                        ?? dockSettings.EndBands.FirstOrDefault(b => b.Id == bandId);

        if (bandSettings == null)
        {
            Logger.LogWarning($"Could not find band settings for band {bandId}");
            return;
        }

        // Remove from all sides (settings and UI)
        dockSettings.StartBands.RemoveAll(b => b.Id == bandId);
        dockSettings.CenterBands.RemoveAll(b => b.Id == bandId);
        dockSettings.EndBands.RemoveAll(b => b.Id == bandId);
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
        // Save ShowLabels for all bands
        foreach (var band in StartItems.Concat(CenterItems).Concat(EndItems))
        {
            band.SaveShowLabels();
        }

        _snapshotStartBands = null;
        _snapshotCenterBands = null;
        _snapshotEndBands = null;
        _snapshotPinnedCommands = null;
        _snapshotBandViewModels = null;
        SettingsModel.SaveSettings(_settingsModel);
        Logger.LogDebug("Saved band order to settings");
    }

    private List<DockBandSettings>? _snapshotStartBands;
    private List<DockBandSettings>? _snapshotCenterBands;
    private List<DockBandSettings>? _snapshotEndBands;
    private List<string>? _snapshotPinnedCommands;
    private Dictionary<string, DockBandViewModel>? _snapshotBandViewModels;

    /// <summary>
    /// Takes a snapshot of the current band order and label settings before editing.
    /// Call this when entering edit mode.
    /// </summary>
    public void SnapshotBandOrder()
    {
        var dockSettings = _settingsModel.DockSettings;
        _snapshotStartBands = dockSettings.StartBands.Select(b => new DockBandSettings { Id = b.Id, ShowLabels = b.ShowLabels }).ToList();
        _snapshotCenterBands = dockSettings.CenterBands.Select(b => new DockBandSettings { Id = b.Id, ShowLabels = b.ShowLabels }).ToList();
        _snapshotEndBands = dockSettings.EndBands.Select(b => new DockBandSettings { Id = b.Id, ShowLabels = b.ShowLabels }).ToList();
        _snapshotPinnedCommands = dockSettings.PinnedCommands.ToList();

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
        if (_snapshotStartBands == null || _snapshotCenterBands == null || _snapshotEndBands == null || _snapshotBandViewModels == null || _snapshotPinnedCommands == null)
        {
            Logger.LogWarning("No snapshot to restore from");
            return;
        }

        // Restore ShowLabels for all snapshotted bands
        foreach (var band in _snapshotBandViewModels.Values)
        {
            band.RestoreShowLabels();
        }

        var dockSettings = _settingsModel.DockSettings;

        // Restore PinnedCommands from snapshot
        dockSettings.PinnedCommands.Clear();
        dockSettings.PinnedCommands.AddRange(_snapshotPinnedCommands);

        // Restore settings from snapshot
        dockSettings.StartBands.Clear();
        dockSettings.CenterBands.Clear();
        dockSettings.EndBands.Clear();

        foreach (var bandSnapshot in _snapshotStartBands)
        {
            var bandSettings = new DockBandSettings { Id = bandSnapshot.Id, ShowLabels = bandSnapshot.ShowLabels };
            dockSettings.StartBands.Add(bandSettings);
        }

        foreach (var bandSnapshot in _snapshotCenterBands)
        {
            var bandSettings = new DockBandSettings { Id = bandSnapshot.Id, ShowLabels = bandSnapshot.ShowLabels };
            dockSettings.CenterBands.Add(bandSettings);
        }

        foreach (var bandSnapshot in _snapshotEndBands)
        {
            var bandSettings = new DockBandSettings { Id = bandSnapshot.Id, ShowLabels = bandSnapshot.ShowLabels };
            dockSettings.EndBands.Add(bandSettings);
        }

        // Rebuild UI collections from restored settings using the snapshotted ViewModels
        RebuildUICollectionsFromSnapshot();

        _snapshotStartBands = null;
        _snapshotCenterBands = null;
        _snapshotEndBands = null;
        _snapshotPinnedCommands = null;
        _snapshotBandViewModels = null;
        Logger.LogDebug("Restored band order from snapshot");
    }

    private void RebuildUICollectionsFromSnapshot()
    {
        if (_snapshotBandViewModels == null)
        {
            return;
        }

        var dockSettings = _settingsModel.DockSettings;

        StartItems.Clear();
        CenterItems.Clear();
        EndItems.Clear();

        foreach (var bandSettings in dockSettings.StartBands)
        {
            if (_snapshotBandViewModels.TryGetValue(bandSettings.Id, out var bandVM))
            {
                StartItems.Add(bandVM);
            }
        }

        foreach (var bandSettings in dockSettings.CenterBands)
        {
            if (_snapshotBandViewModels.TryGetValue(bandSettings.Id, out var bandVM))
            {
                CenterItems.Add(bandVM);
            }
        }

        foreach (var bandSettings in dockSettings.EndBands)
        {
            if (_snapshotBandViewModels.TryGetValue(bandSettings.Id, out var bandVM))
            {
                EndItems.Add(bandVM);
            }
        }
    }

    private void RebuildUICollections()
    {
        var dockSettings = _settingsModel.DockSettings;

        // Create a lookup of all current band ViewModels
        var allBands = StartItems.Concat(CenterItems).Concat(EndItems).ToDictionary(b => b.Id);

        StartItems.Clear();
        CenterItems.Clear();
        EndItems.Clear();

        foreach (var bandSettings in dockSettings.StartBands)
        {
            if (allBands.TryGetValue(bandSettings.Id, out var bandVM))
            {
                StartItems.Add(bandVM);
            }
        }

        foreach (var bandSettings in dockSettings.CenterBands)
        {
            if (allBands.TryGetValue(bandSettings.Id, out var bandVM))
            {
                CenterItems.Add(bandVM);
            }
        }

        foreach (var bandSettings in dockSettings.EndBands)
        {
            if (allBands.TryGetValue(bandSettings.Id, out var bandVM))
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
        var bandSettings = new DockBandSettings { Id = bandId, ShowLabels = null };
        var dockSettings = _settingsModel.DockSettings;

        // If this is not an explicit dock band (i.e., it's from TopLevelCommands),
        // add it to PinnedCommands so it gets loaded as a dock band on restart
        if (!topLevel.IsDockBand && !dockSettings.PinnedCommands.Contains(bandId))
        {
            dockSettings.PinnedCommands.Add(bandId);
        }

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

        Logger.LogDebug($"Added band {bandId} to {targetSide} (not saved yet)");
    }

    /// <summary>
    /// Unpins a band from the dock, removing it from whichever section it's in.
    /// Does not save to disk - call SaveBandOrder() when done editing.
    /// </summary>
    public void UnpinBand(DockBandViewModel band)
    {
        var bandId = band.Id;
        var dockSettings = _settingsModel.DockSettings;

        // Remove from settings
        dockSettings.StartBands.RemoveAll(b => b.Id == bandId);
        dockSettings.CenterBands.RemoveAll(b => b.Id == bandId);
        dockSettings.EndBands.RemoveAll(b => b.Id == bandId);

        // Also remove from PinnedCommands if it was pinned from TopLevelCommands
        dockSettings.PinnedCommands.Remove(bandId);

        // Remove from UI collections
        StartItems.Remove(band);
        CenterItems.Remove(band);
        EndItems.Remove(band);

        Logger.LogDebug($"Unpinned band {bandId} (not saved yet)");
    }

    public void ShowException(Exception ex, string? extensionHint = null)
    {
        var extensionText = extensionHint ?? "<unknown>";
        CoreLogger.LogError($"Error in extension {extensionText}", ex);
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
        var vm = new CommandItemViewModel(new(model), new(this));
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
                Name = "Edit dock", // TODO!Loc
                Icon = Icons.EditIcon,
            };

            var openSettingsCommand = new AnonymousCommand(
                action: () =>
                {
                    WeakReferenceMessenger.Default.Send(new OpenSettingsMessage("Dock"));
                })
            {
                Name = "Dock settings", // TODO!Loc
                Icon = Icons.SettingsIcon,
            };

            MoreCommands = new CommandContextItem[]
            {
                new CommandContextItem(editDockCommand),
                new CommandContextItem(openSettingsCommand),
            };
        }
    }
}

#pragma warning restore SA1402 // File may only contain a single type
