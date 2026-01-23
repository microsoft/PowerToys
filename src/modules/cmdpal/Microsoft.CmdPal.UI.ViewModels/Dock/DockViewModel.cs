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

    public TaskScheduler Scheduler { get; }

    public ObservableCollection<DockBandViewModel> StartItems { get; } = new();

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
        DockBandViewModel band = new(commandItem, new(this), bandSettings, _settings);
        band.InitializeProperties(); // TODO! make async
        return band;
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
                        ?? dockSettings.EndBands.FirstOrDefault(b => b.Id == bandId);

        if (bandSettings == null)
        {
            return;
        }

        // Remove from both settings lists
        dockSettings.StartBands.RemoveAll(b => b.Id == bandId);
        dockSettings.EndBands.RemoveAll(b => b.Id == bandId);

        // Add to target settings list at the correct index
        var targetSettings = targetSide == DockPinSide.Start ? dockSettings.StartBands : dockSettings.EndBands;
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
                        ?? dockSettings.EndBands.FirstOrDefault(b => b.Id == bandId);

        if (bandSettings == null)
        {
            Logger.LogWarning($"Could not find band settings for band {bandId}");
            return;
        }

        // Remove from both sides (settings and UI)
        dockSettings.StartBands.RemoveAll(b => b.Id == bandId);
        dockSettings.EndBands.RemoveAll(b => b.Id == bandId);
        StartItems.Remove(band);
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
    /// Saves the current band order to settings.
    /// Call this when exiting edit mode.
    /// </summary>
    public void SaveBandOrder()
    {
        _snapshotStartBands = null;
        _snapshotEndBands = null;
        SettingsModel.SaveSettings(_settingsModel);
        Logger.LogDebug("Saved band order to settings");
    }

    private List<DockBandSettings>? _snapshotStartBands;
    private List<DockBandSettings>? _snapshotEndBands;

    /// <summary>
    /// Takes a snapshot of the current band order before editing.
    /// Call this when entering edit mode.
    /// </summary>
    public void SnapshotBandOrder()
    {
        var dockSettings = _settingsModel.DockSettings;
        _snapshotStartBands = dockSettings.StartBands.Select(b => new DockBandSettings { Id = b.Id }).ToList();
        _snapshotEndBands = dockSettings.EndBands.Select(b => new DockBandSettings { Id = b.Id }).ToList();
        Logger.LogDebug($"Snapshot taken: {_snapshotStartBands.Count} start bands, {_snapshotEndBands.Count} end bands");
    }

    /// <summary>
    /// Restores the band order from the snapshot taken when entering edit mode.
    /// Call this when discarding edit mode changes.
    /// </summary>
    public void RestoreBandOrder()
    {
        if (_snapshotStartBands == null || _snapshotEndBands == null)
        {
            Logger.LogWarning("No snapshot to restore from");
            return;
        }

        var dockSettings = _settingsModel.DockSettings;

        // Restore settings from snapshot
        dockSettings.StartBands.Clear();
        dockSettings.EndBands.Clear();

        foreach (var bandSnapshot in _snapshotStartBands)
        {
            var bandSettings = new DockBandSettings { Id = bandSnapshot.Id };
            dockSettings.StartBands.Add(bandSettings);
        }

        foreach (var bandSnapshot in _snapshotEndBands)
        {
            var bandSettings = new DockBandSettings { Id = bandSnapshot.Id };
            dockSettings.EndBands.Add(bandSettings);
        }

        // Rebuild UI collections from restored settings
        RebuildUICollections();

        _snapshotStartBands = null;
        _snapshotEndBands = null;
        Logger.LogDebug("Restored band order from snapshot");
    }

    private void RebuildUICollections()
    {
        var dockSettings = _settingsModel.DockSettings;

        // Create a lookup of all current band ViewModels
        var allBands = StartItems.Concat(EndItems).ToDictionary(b => b.Id);

        StartItems.Clear();
        EndItems.Clear();

        foreach (var bandSettings in dockSettings.StartBands)
        {
            if (allBands.TryGetValue(bandSettings.Id, out var bandVM))
            {
                StartItems.Add(bandVM);
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
