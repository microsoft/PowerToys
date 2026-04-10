// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.Extensions.Logging;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// Thin event-driven aggregator that subscribes to <see cref="IExtensionService"/>
/// instances and merges their command / dock-band output into observable collections.
/// All loading logic lives in <see cref="BuiltInExtensionService"/> and
/// <see cref="WinRTExtensionService"/>; this class only reacts to their events.
/// </summary>
public sealed partial class TopLevelCommandManager : ObservableObject,
    IRecipient<ReloadCommandsMessage>,
    IRecipient<PinCommandItemMessage>,
    IRecipient<UnpinCommandItemMessage>,
    IRecipient<PinToDockMessage>,
    IPageContext,
    IDisposable
{
    private readonly IEnumerable<IExtensionService> _extensionServices;
    private readonly ISettingsService _settingsService;
    private readonly TaskScheduler _taskScheduler;
    private readonly ILogger<TopLevelCommandManager> _logger;

    private readonly List<CommandProviderWrapper> _commandProviderWrappers = [];
    private readonly Lock _commandProvidersLock = new();

    // WATCH OUT: if you add code that locks _commandProvidersLock, always
    // lock _commandProvidersLock BEFORE _dockBandsLock, or you risk deadlock.
    private readonly Lock _dockBandsLock = new();
    private readonly SupersedingAsyncGate _reloadCommandsGate;

    public TopLevelCommandManager(
        IEnumerable<IExtensionService> extensionServices,
        ISettingsService settingsService,
        TaskScheduler taskScheduler,
        ILogger<TopLevelCommandManager> logger)
    {
        _extensionServices = extensionServices;
        _settingsService = settingsService;
        _taskScheduler = taskScheduler;
        _logger = logger;

        WeakReferenceMessenger.Default.Register<ReloadCommandsMessage>(this);
        WeakReferenceMessenger.Default.Register<PinCommandItemMessage>(this);
        WeakReferenceMessenger.Default.Register<UnpinCommandItemMessage>(this);
        WeakReferenceMessenger.Default.Register<PinToDockMessage>(this);

        _reloadCommandsGate = new(ReloadAllCommandsAsyncCore);
    }

    // ── Observable collections ──────────────────────────────────────────
    public ObservableCollection<TopLevelViewModel> TopLevelCommands { get; set; } = [];

    public ObservableCollection<TopLevelViewModel> DockBands { get; set; } = [];

    [ObservableProperty]
    public partial bool IsLoading { get; private set; } = true;

    public IEnumerable<CommandProviderWrapper> CommandProviders
    {
        get
        {
            lock (_commandProvidersLock)
            {
                return _commandProviderWrappers.ToList();
            }
        }
    }

    // ── IPageContext (explicit) ─────────────────────────────────────────
    TaskScheduler IPageContext.Scheduler => _taskScheduler;

    ICommandProviderContext IPageContext.ProviderContext => CommandProviderContext.Empty;

    void IPageContext.ShowException(Exception ex, string? extensionHint)
    {
        var message = DiagnosticsHelper.BuildExceptionMessage(ex, extensionHint ?? "TopLevelCommandManager");
        LogExceptionOccurred(message);
    }

    // ── Lifecycle ───────────────────────────────────────────────────────

    /// <summary>
    /// Subscribes to every <see cref="IExtensionService"/> and signals them to
    /// start loading.  Commands arrive asynchronously through events.
    /// </summary>
    [RelayCommand]
    public async Task LoadExtensionsAsync()
    {
        lock (_commandProvidersLock)
        {
            _commandProviderWrappers.Clear();
        }

        var weakSelf = new WeakReference<IPageContext>(this);

        foreach (var service in _extensionServices)
        {
            UnsubscribeFromService(service);
            SubscribeToService(service);

            _ = Task.Run(async () =>
            {
                await service.SignalStartExtensionsAsync(weakSelf);
            });
        }

        IsLoading = false;

        // Send on the current thread; receivers should marshal to UI if needed
        WeakReferenceMessenger.Default.Send<ReloadFinishedMessage>();

        await Task.CompletedTask;
    }

    public async Task ReloadAllCommandsAsync()
    {
        // Gate ensures that the reload is serialized; if multiple callers
        // request a reload, only the first and the last one execute.
        await _reloadCommandsGate.ExecuteAsync(CancellationToken.None);
    }

    private async Task ReloadAllCommandsAsyncCore(CancellationToken cancellationToken)
    {
        IsLoading = true;

        foreach (var service in _extensionServices)
        {
            await service.SignalStopExtensionsAsync().ConfigureAwait(false);
        }

        lock (TopLevelCommands)
        {
            TopLevelCommands.Clear();
        }

        lock (_dockBandsLock)
        {
            DockBands.Clear();
        }

        _ = Task.Run(LoadExtensionsAsync, cancellationToken);
    }

    // ── Event handlers (IExtensionService) ─────────────────────────────
    private void ExtensionService_OnProviderAdded(IExtensionService sender, IEnumerable<CommandProviderWrapper> wrappers)
    {
        lock (_commandProvidersLock)
        {
            foreach (var w in wrappers)
            {
                _commandProviderWrappers.Add(w);
            }
        }
    }

    private void ExtensionService_OnProviderRemoved(IExtensionService sender, IEnumerable<CommandProviderWrapper> wrappers)
    {
        lock (_commandProvidersLock)
        {
            foreach (var w in wrappers)
            {
                _commandProviderWrappers.Remove(w);
            }
        }
    }

    private void ExtensionService_OnCommandsAdded(CommandProviderWrapper wrapper, IEnumerable<TopLevelViewModel> items)
    {
        _ = Task.Run(async () =>
        {
            await Task.Factory.StartNew(
                () =>
                {
                    lock (TopLevelCommands)
                    {
                        foreach (var item in items)
                        {
                            if (item.IsDockBand)
                            {
                                lock (_dockBandsLock)
                                {
                                    DockBands.Add(item);
                                }
                            }
                            else
                            {
                                TopLevelCommands.Add(item);
                            }
                        }
                    }
                },
                CancellationToken.None,
                TaskCreationOptions.None,
                _taskScheduler);
        });
    }

    private void ExtensionService_OnCommandsRemoved(CommandProviderWrapper wrapper, IEnumerable<TopLevelViewModel> items)
    {
        _ = Task.Run(async () =>
        {
            await Task.Factory.StartNew(
                () =>
                {
                    lock (TopLevelCommands)
                    {
                        foreach (var item in items)
                        {
                            if (item.IsDockBand)
                            {
                                lock (_dockBandsLock)
                                {
                                    DockBands.Remove(item);
                                }
                            }
                            else
                            {
                                TopLevelCommands.Remove(item);
                            }
                        }
                    }
                },
                CancellationToken.None,
                TaskCreationOptions.None,
                _taskScheduler);
        });
    }

    // ── Lookup helpers ──────────────────────────────────────────────────
    public TopLevelViewModel? LookupCommand(string id)
    {
        lock (TopLevelCommands)
        {
            foreach (var command in TopLevelCommands)
            {
                if (command.Id == id)
                {
                    return command;
                }
            }
        }

        return null;
    }

    public TopLevelViewModel? LookupDockBand(string id)
    {
        lock (_dockBandsLock)
        {
            foreach (var band in DockBands)
            {
                if (band.Id == id)
                {
                    return band;
                }
            }
        }

        return null;
    }

    public List<TopLevelViewModel> GetDockBandsSnapshot()
    {
        lock (_dockBandsLock)
        {
            return [.. DockBands];
        }
    }

    public CommandProviderWrapper? LookupProvider(string providerId)
    {
        lock (_commandProvidersLock)
        {
            return _commandProviderWrappers.FirstOrDefault(w => w.ProviderId == providerId);
        }
    }

    internal bool IsProviderActive(string id)
    {
        lock (_commandProvidersLock)
        {
            return _commandProviderWrappers.Any(wrapper => wrapper.Id == id && wrapper.IsActive);
        }
    }

    // ── Message handlers ────────────────────────────────────────────────
    public void Receive(ReloadCommandsMessage message) =>
        _ = ReloadAllCommandsAsync();

    public void Receive(PinCommandItemMessage message)
    {
        var wrapper = LookupProvider(message.ProviderId);
        wrapper?.PinCommand(message.CommandId, _settingsService);
    }

    public void Receive(UnpinCommandItemMessage message)
    {
        var wrapper = LookupProvider(message.ProviderId);
        wrapper?.UnpinCommand(message.CommandId, _settingsService);
    }

    public void Receive(PinToDockMessage message)
    {
        if (LookupProvider(message.ProviderId) is CommandProviderWrapper wrapper)
        {
            if (message.Pin)
            {
                wrapper.PinDockBand(message.CommandId, _settingsService, message.Side, message.ShowTitles, message.ShowSubtitles);
            }
            else
            {
                wrapper.UnpinDockBand(message.CommandId, _settingsService);
            }
        }
    }

    // ── DockBands management ────────────────────────────────────────────
    internal void PinDockBand(TopLevelViewModel bandVm)
    {
        // Snapshot providers before acquiring _dockBandsLock to respect lock ordering:
        // _commandProvidersLock → _dockBandsLock (never the reverse)
        var providers = CommandProviders;

        lock (_dockBandsLock)
        {
            foreach (var existing in DockBands)
            {
                if (existing.Id == bandVm.Id)
                {
                    LogDockBandAlreadyPinned(bandVm.Id);
                    return;
                }
            }

            LogAttemptingPinDockBand(bandVm.Id, bandVm.CommandProviderId);
            var providerId = bandVm.CommandProviderId;
            var foundProvider = false;

            foreach (var provider in providers)
            {
                if (provider.Id == providerId)
                {
                    LogFoundProviderForDockBand(providerId, bandVm.Id);
                    provider.PinDockBand(bandVm);
                    foundProvider = true;
                    break;
                }
            }

            if (!foundProvider)
            {
                LogProviderNotFound(providerId, bandVm.Id);
            }
            else if (!DockBands.Any(b => b.Id == bandVm.Id))
            {
                DockBands.Add(bandVm);
            }
        }
    }

    // ── Subscription helpers ────────────────────────────────────────────
    private void SubscribeToService(IExtensionService service)
    {
        service.OnCommandProviderAdded += ExtensionService_OnProviderAdded;
        service.OnCommandProviderRemoved += ExtensionService_OnProviderRemoved;
        service.OnCommandsAdded += ExtensionService_OnCommandsAdded;
        service.OnCommandsRemoved += ExtensionService_OnCommandsRemoved;
    }

    private void UnsubscribeFromService(IExtensionService service)
    {
        service.OnCommandProviderAdded -= ExtensionService_OnProviderAdded;
        service.OnCommandProviderRemoved -= ExtensionService_OnProviderRemoved;
        service.OnCommandsAdded -= ExtensionService_OnCommandsAdded;
        service.OnCommandsRemoved -= ExtensionService_OnCommandsRemoved;
    }

    // ── Dispose ─────────────────────────────────────────────────────────
    public void Dispose()
    {
        foreach (var service in _extensionServices)
        {
            UnsubscribeFromService(service);
        }

        _reloadCommandsGate.Dispose();
        GC.SuppressFinalize(this);
    }

    // ── LoggerMessage source-generated methods ──────────────────────────
    [LoggerMessage(Level = LogLevel.Error, Message = "{Message}")]
    partial void LogExceptionOccurred(string message);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Dock band '{BandId}' is already pinned")]
    partial void LogDockBandAlreadyPinned(string bandId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Attempting to pin dock band '{BandId}' from provider '{ProviderId}'")]
    partial void LogAttemptingPinDockBand(string bandId, string providerId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Found provider '{ProviderId}' to pin dock band '{BandId}'")]
    partial void LogFoundProviderForDockBand(string providerId, string bandId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not find provider '{ProviderId}' to pin dock band '{BandId}'")]
    partial void LogProviderNotFound(string providerId, string bandId);
}
