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

    private DockSettings _settings;

    // private DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    // private DispatcherQueue _updateWindowsQueue = DispatcherQueueController.CreateOnDedicatedThread().DispatcherQueue;
    public TaskScheduler Scheduler { get; }

    public ObservableCollection<DockBandViewModel> StartItems { get; } = new();

    public ObservableCollection<DockBandViewModel> EndItems { get; } = new();

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

    private DockBandViewModel CreateBandItem(DockBandSettings bandSettings, CommandItemViewModel commandItem)
    {
        DockBandViewModel band = new(commandItem, new(this), bandSettings);
        band.InitializeProperties(); // TODO! make async
        return band;
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

#pragma warning restore SA1402 // File may only contain a single type
