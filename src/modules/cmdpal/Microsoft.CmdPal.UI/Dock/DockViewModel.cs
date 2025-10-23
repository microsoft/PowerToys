// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.Core.Common;
using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.UI.Dispatching;

namespace Microsoft.CmdPal.UI.Dock;

internal sealed partial class DockViewModel : IDisposable, IRecipient<CommandsReloadedMessage>
{
    private readonly TopLevelCommandManager _topLevelCommandManager;

    // private TaskbarWindowsService _taskbarWindows;
    // private Settings _settings;
    private DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    private DispatcherQueue _updateWindowsQueue = DispatcherQueueController.CreateOnDedicatedThread().DispatcherQueue;

    // TODO! make these DockBandViewModel
    public ObservableCollection<CommandItemViewModel> StartItems { get; } = new();

    public ObservableCollection<CommandItemViewModel> EndItems { get; } = new();

    public DockViewModel(TopLevelCommandManager tlcManager, SettingsModel settings)
    {
        _topLevelCommandManager = tlcManager;
        WeakReferenceMessenger.Default.Register<CommandsReloadedMessage>(this);
    }

    private static string[] _startCommands = ["com.microsoft.cmdpal.windowwalker", "com.microsoft.cmdpal.timedate"];

    private void SetupBands()
    {
        List<CommandItemViewModel> newBands = new();
        foreach (var commandId in _startCommands)
        {
            var topLevelCommand = _topLevelCommandManager.LookupCommand(commandId);
            if (topLevelCommand is not null)
            {
                // var band = CreateBandItem(topLevelCommand);
                newBands.Add(topLevelCommand.ItemViewModel);
            }
        }

        var beforeCount = StartItems.Count;
        var afterCount = newBands.Count;

        _dispatcherQueue.TryEnqueue(() =>
        {
            ListHelpers.InPlaceUpdateList(StartItems, newBands, out var removed);
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
}
#pragma warning restore SA1402 // File may only contain a single type
