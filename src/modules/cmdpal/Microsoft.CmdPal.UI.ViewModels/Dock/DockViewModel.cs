// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.Core.Common;
using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.UI.Dispatching;

namespace Microsoft.CmdPal.UI.ViewModels.Dock;

public sealed partial class DockViewModel : IDisposable, IRecipient<CommandsReloadedMessage>, IPageContext
{
    private readonly TopLevelCommandManager _topLevelCommandManager;

    // private Settings _settings;
    private DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    private DispatcherQueue _updateWindowsQueue = DispatcherQueueController.CreateOnDedicatedThread().DispatcherQueue;

    public ObservableCollection<DockBandViewModel> StartItems { get; } = new();

    public ObservableCollection<DockBandViewModel> EndItems { get; } = new();

    public DockViewModel(TopLevelCommandManager tlcManager, SettingsModel settings, TaskScheduler scheduler)
    {
        _topLevelCommandManager = tlcManager;
        Scheduler = scheduler;
        WeakReferenceMessenger.Default.Register<CommandsReloadedMessage>(this);
    }

    private static string[] _startCommands = [
        "com.microsoft.cmdpal.windowwalker.dockband",
    ];

    private static string[] _endCommands = [
        "com.crloewen.performanceMonitor.dockband",
        "com.microsoft.cmdpal.clipboardHistory.Band",
        "com.zadjii.virtualDesktops.band",
        "com.microsoft.cmdpal.timedate.dockband",
    ];

    private void SetupBands()
    {
        SetupBands(_startCommands, StartItems);
        SetupBands(_endCommands, EndItems);
    }

    private void SetupBands(string[] bandIds, ObservableCollection<DockBandViewModel> target)
    {
        List<DockBandViewModel> newBands = new();
        foreach (var commandId in bandIds)
        {
            var topLevelCommand = _topLevelCommandManager.LookupDockBand(commandId);

            // TODO! temp hack: fallback to looking up a top-level command
            // remove this once the API is added
            if (topLevelCommand is null)
            {
                topLevelCommand = _topLevelCommandManager.LookupCommand(commandId);
            }

            if (topLevelCommand is not null)
            {
                var band = CreateBandItem(topLevelCommand.ItemViewModel);
                newBands.Add(band);
            }
        }

        var beforeCount = target.Count;
        var afterCount = newBands.Count;

        _dispatcherQueue.TryEnqueue(() =>
        {
            ListHelpers.InPlaceUpdateList(target, newBands, out var removed);
            Logger.LogDebug($"({beforeCount}) -> ({afterCount}), Removed {removed?.Count ?? 0} items");
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

    private DockBandViewModel CreateBandItem(CommandItemViewModel commandItem)
    {
        DockBandViewModel band = new(commandItem, new(this));
        band.InitializeProperties(); // TODO! make async
        return band;
    }

    public TaskScheduler Scheduler { get; }

    public void ShowException(Exception ex, string? extensionHint = null)
    {
        var extensionText = extensionHint ?? "<unknown>";
        CoreLogger.LogError($"Error in extension {extensionText}", ex);
    }
}

#pragma warning restore SA1402 // File may only contain a single type
