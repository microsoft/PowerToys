// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
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

    private DockSettings _settings;

    // private DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    // private DispatcherQueue _updateWindowsQueue = DispatcherQueueController.CreateOnDedicatedThread().DispatcherQueue;
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

            // TODO! temp hack: fallback to looking up a top-level command
            // remove this once the API is added
            if (topLevelCommand is null)
            {
                Logger.LogWarning($"Temporary fallback to loading top-level command '{commandId}'");
                topLevelCommand = _topLevelCommandManager.LookupCommand(commandId);
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
            var openSettingsCommand = new AnonymousCommand(
                action: () =>
                {
                    WeakReferenceMessenger.Default.Send(new OpenSettingsMessage("Dock"));
                })
            {
                Name = "Customize", // TODO!Loc
                Icon = Icons.SettingsIcon,
            };

            MoreCommands = new CommandContextItem[]
            {
                new CommandContextItem(openSettingsCommand),
            };
        }
    }
}

#pragma warning disable SA1402 // File may only contain a single type

public enum DockPinSide
{
    None,
    Start,
    End,
}

public enum ShowLabelsOption
{
    Default,
    ShowLabels,
    HideLabels,
}

// public class DockSettingsViewModel : ObservableObject
// {
//     private readonly DockSettings _settingsModel;
//     public DockSettingsViewModel(DockSettings settingsModel)
//     {
//         _settingsModel = settingsModel;
//     }
// }
public partial class DockBandSettingsViewModel : ObservableObject
{
    private readonly SettingsModel _settingsModel;
    private readonly DockBandSettings _dockSettingsModel;
    private readonly TopLevelViewModel _adapter;
    private readonly DockBandViewModel? _bandViewModel;

    public string Title => _adapter.Title;

    public string Description
    {
        get
        {
            // TODO! we should have a way of saying "pinned from {extension}" vs
            // just a band that's from an extension
            List<string> parts = [_adapter.ExtensionName];

            // Add the number of items in the band
            var itemCount = NumItemsInBand();
            if (itemCount > 0)
            {
                var itemsString = itemCount == 1 ? "1 item" : $"{itemCount} items"; // TODO!Loc
                parts.Add(itemsString);
            }

            return string.Join(" - ", parts);
        }
    }

    public string ProviderId => _adapter.CommandProviderId;

    public IconInfoViewModel Icon => _adapter.IconViewModel;

    public ShowLabelsOption ShowLabels
    {
        get
        {
            if (_dockSettingsModel.ShowLabels == null)
            {
                return ShowLabelsOption.Default;
            }

            return _dockSettingsModel.ShowLabels.Value ? ShowLabelsOption.ShowLabels : ShowLabelsOption.HideLabels;
        }

        set
        {
            _dockSettingsModel.ShowLabels = value switch
            {
                ShowLabelsOption.Default => null,
                ShowLabelsOption.ShowLabels => true,
                ShowLabelsOption.HideLabels => false,
                _ => null,
            };
            Save();
        }
    }

    // used to map to ComboBox selection
    public int ShowLabelsIndex
    {
        get => (int)ShowLabels;
        set => ShowLabels = (ShowLabelsOption)value;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PinSideIndex))]
    private partial DockPinSide PinSide { get; set; } // TODO! persist to settings

    public int PinSideIndex
    {
        get => (int)PinSide;
        set => PinSide = (DockPinSide)value;
    }

    public DockBandSettingsViewModel(
        DockBandSettings dockSettingsModel,
        TopLevelViewModel topLevelAdapter,
        DockBandViewModel? bandViewModel,
        SettingsModel settingsModel)
    {
        _dockSettingsModel = dockSettingsModel;
        _adapter = topLevelAdapter;
        _bandViewModel = bandViewModel;
        _settingsModel = settingsModel;
        PinSide = FetchPinSide();
    }

    private DockPinSide FetchPinSide()
    {
        var dockSettings = _settingsModel.DockSettings;
        var inStart = dockSettings.StartBands.Any(b => b.Id == _dockSettingsModel.Id);
        if (inStart)
        {
            return DockPinSide.Start;
        }

        var inEnd = dockSettings.EndBands.Any(b => b.Id == _dockSettingsModel.Id);
        if (inEnd)
        {
            return DockPinSide.End;
        }

        return DockPinSide.None;
    }

    private int NumItemsInBand()
    {
        var bandVm = _bandViewModel;
        if (bandVm is null)
        {
            return 0;
        }

        return _bandViewModel!.Items.Count;
    }

    private void Save()
    {
        SettingsModel.SaveSettings(_settingsModel);
    }

    partial void OnPinSideChanged(DockPinSide value)
    {
        var dockSettings = _settingsModel.DockSettings;

        // Remove from both sides first
        dockSettings.StartBands.RemoveAll(b => b.Id == _dockSettingsModel.Id);
        dockSettings.EndBands.RemoveAll(b => b.Id == _dockSettingsModel.Id);

        // Add to the selected side
        switch (value)
        {
            case DockPinSide.Start:
                dockSettings.StartBands.Add(_dockSettingsModel);
                break;
            case DockPinSide.End:
                dockSettings.EndBands.Add(_dockSettingsModel);
                break;
            case DockPinSide.None:
            default:
                // Do nothing
                break;
        }

        Save();
    }
}
#pragma warning restore SA1402 // File may only contain a single type
