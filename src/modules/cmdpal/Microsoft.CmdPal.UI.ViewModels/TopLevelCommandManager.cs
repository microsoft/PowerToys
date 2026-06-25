// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed partial class TopLevelCommandManager : ObservableObject,
    IRecipient<ReloadCommandsMessage>,
    IRecipient<ProviderEnabledStateChangedMessage>,
    IRecipient<PinCommandItemMessage>,
    IRecipient<UnpinCommandItemMessage>,
    IRecipient<PinToDockMessage>,
    IDisposable
{
    private static readonly TimeSpan CommandLoadTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan BackgroundCommandLoadTimeout = TimeSpan.FromSeconds(60);

    private readonly IServiceProvider _serviceProvider;
    private readonly IEnumerable<IExtensionService> _extensionServices;
    private readonly TaskScheduler _taskScheduler;

    private readonly List<CommandProviderWrapper> _commandProviders = [];
    private readonly Lock _commandProvidersLock = new();

    // watch out: if you add code that locks CommandProviders, be sure to always
    // lock CommandProviders before locking DockBands, or you will cause a
    // deadlock.
    private readonly Lock _dockBandsLock = new();
    private readonly SupersedingAsyncGate _reloadCommandsGate;
    private CancellationTokenSource _extensionLoadCts = new();
    private CancellationToken _currentExtensionLoadCancellationToken;

    private HashSet<(string ProviderId, string CommandId)> _pinnedCommandSet = [];

    public TopLevelCommandManager(IServiceProvider serviceProvider, IEnumerable<IExtensionService> extensionServices)
    {
        _serviceProvider = serviceProvider;
        _extensionServices = extensionServices;
        _currentExtensionLoadCancellationToken = _extensionLoadCts.Token;
        _taskScheduler = _serviceProvider.GetService<TaskScheduler>()!;
        WeakReferenceMessenger.Default.Register<ReloadCommandsMessage>(this);
        WeakReferenceMessenger.Default.Register<ProviderEnabledStateChangedMessage>(this);
        WeakReferenceMessenger.Default.Register<PinCommandItemMessage>(this);
        WeakReferenceMessenger.Default.Register<UnpinCommandItemMessage>(this);
        WeakReferenceMessenger.Default.Register<PinToDockMessage>(this);
        _reloadCommandsGate = new(ReloadAllCommandsAsyncCore);
        RebuildPinnedCache();

        foreach (var service in _extensionServices)
        {
            service.OnProviderAdded += ExtensionService_OnProviderAdded;
            service.OnProviderRemoved += ExtensionService_OnProviderRemoved;
        }
    }

    public ObservableCollection<PinnedCommandSettings> PinnedCommands { get; } = [];

    public ObservableCollection<TopLevelViewModel> TopLevelCommands { get; set; } = [];

    // DockBands uses a custom collection so that bulk rewrites (see
    // UpdateCommandsForProvider) raise a single Reset notification instead of
    // one event per inserted/removed/moved item. The dock subscribes to this
    // collection and does a full rebuild per event, so collapsing the burst
    // here avoids dozens of redundant rebuilds for one provider reload.
    public DockBandsCollection DockBands { get; } = new();

    [ObservableProperty]
    public partial bool IsLoading { get; private set; } = true;

    public IEnumerable<CommandProviderWrapper> CommandProviders
    {
        get
        {
            lock (_commandProvidersLock)
            {
                return _commandProviders.ToList();
            }
        }
    }

    internal bool IsPinned(string providerId, string commandId)
    {
        return _pinnedCommandSet.Contains((providerId, commandId));
    }

    internal void RebuildPinnedCache()
    {
        var settings = _serviceProvider.GetRequiredService<ISettingsService>().Settings;
        _pinnedCommandSet = new(settings.PinnedCommands.Select(p => (p.ProviderId, p.CommandId)));
        ListHelpers.InPlaceUpdateList(PinnedCommands, settings.PinnedCommands);
    }

    // May be called from a background thread
    private async Task<TopLevelObjectSets> LoadTopLevelCommandsFromProvider(CommandProviderWrapper commandProvider)
    {
        await commandProvider.LoadTopLevelCommands(_serviceProvider);

        var commands = await Task.Factory.StartNew(
            () =>
            {
                List<TopLevelViewModel> commands = [];
                List<TopLevelViewModel> bands = [];
                foreach (var item in commandProvider.TopLevelItems)
                {
                    commands.Add(item);
                }

                foreach (var item in commandProvider.FallbackItems)
                {
                    if (item.IsEnabled)
                    {
                        commands.Add(item);
                    }
                }

                foreach (var item in commandProvider.DockBandItems)
                {
                    bands.Add(item);
                }

                var commandsCount = commands.Count;
                var bandsCount = bands.Count;
                Logger.LogDebug($"{commandProvider.ProviderId}: Loaded {commandsCount} commands, {bandsCount} bands");
                return new TopLevelObjectSets(commands, bands);
            },
            CancellationToken.None,
            TaskCreationOptions.None,
            _taskScheduler);

        commandProvider.CommandsChanged -= CommandProvider_CommandsChanged;
        commandProvider.CommandsChanged += CommandProvider_CommandsChanged;

        return commands;
    }

    // By all accounts, we're already on a background thread (the COM call
    // to handle the event shouldn't be on the main thread.). But just to
    // be sure we don't block the caller, hop off this thread
    private void CommandProvider_CommandsChanged(CommandProviderWrapper sender, IItemsChangedEventArgs args)
    {
        _ = Task.Run(async () => await UpdateCommandsForProvider(sender, args));
    }

    /// <summary>
    /// Called when a command provider raises its ItemsChanged event. We'll
    /// remove the old commands from the top-level list and try to put the new
    /// ones in the same place in the list.
    /// </summary>
    /// <param name="sender">The provider who's commands changed</param>
    /// <param name="args">the ItemsChangedEvent the provider raised</param>
    /// <returns>an awaitable task</returns>
    private async Task UpdateCommandsForProvider(CommandProviderWrapper sender, IItemsChangedEventArgs args)
    {
        await sender.LoadTopLevelCommands(_serviceProvider);

        List<TopLevelViewModel> newItems = [.. sender.TopLevelItems];
        foreach (var i in sender.FallbackItems)
        {
            if (i.IsEnabled)
            {
                newItems.Add(i);
            }
        }

        List<TopLevelViewModel> newBands = [.. sender.DockBandItems];

        // modify the TopLevelCommands under shared lock; event if we clone it, we don't want
        // TopLevelCommands to get modified while we're working on it. Otherwise, we might
        // out clone would be stale at the end of this method.
        lock (TopLevelCommands)
        {
            // Work on a clone of the list, so that we can just do one atomic
            // update to the actual observable list at the end
            // TODO: just added a lock around all of this anyway, but keeping the clone
            // while looking on some other ways to improve this; can be removed later.
            List<TopLevelViewModel> clone = [.. TopLevelCommands];

            var startIndex = FindIndexForFirstProviderItem(clone, sender.ProviderId);
            clone.RemoveAll(item => item.CommandProviderId == sender.ProviderId);
            clone.InsertRange(startIndex, newItems);

            ListHelpers.InPlaceUpdateList(TopLevelCommands, clone);
        }

        lock (_dockBandsLock)
        {
            // Same idea as TopLevelCommands above, but we deliberately use
            // ReplaceWith so the dock only sees one CollectionChanged event
            // for the whole rewrite instead of one per item.
            List<TopLevelViewModel> dockClone = [.. DockBands];
            var dockStartIndex = FindIndexForFirstProviderItem(dockClone, sender.ProviderId);
            dockClone.RemoveAll(item => item.CommandProviderId == sender.ProviderId);
            dockClone.InsertRange(dockStartIndex, newBands);
            DockBands.ReplaceWith(dockClone);
        }

        return;

        static int FindIndexForFirstProviderItem(List<TopLevelViewModel> topLevelItems, string providerId)
        {
            // Tricky: all Commands from a single provider get added to the
            // top-level list all together, in a row. So if we find just the first
            // one, we can slice it out and insert the new ones there.
            for (var i = 0; i < topLevelItems.Count; i++)
            {
                var wrapper = topLevelItems[i];
                try
                {
                    if (providerId == wrapper.CommandProviderId)
                    {
                        return i;
                    }
                }
                catch
                {
                }
            }

            // If we didn't find any, then we just append the new commands to the end of the list.
            return topLevelItems.Count;
        }
    }

    public async Task ReloadAllCommandsAsync()
    {
        // gate ensures that the reload is serialized and if multiple calls
        // request a reload, only the first and the last one will be executed.
        await _reloadCommandsGate.ExecuteAsync(CancellationToken.None);
    }

    /// <summary>
    /// Loads only built-in (in-process) command providers. This is fast and
    /// suitable for the initial pre-load phase so the UI appears immediately.
    /// </summary>
    public async Task LoadBuiltInProvidersAsync()
    {
        var ct = _currentExtensionLoadCancellationToken;
        foreach (var service in _extensionServices.OfType<BuiltInExtensionService>())
        {
            var wrappers = await service.LoadProvidersAsync(ct).ConfigureAwait(false);
            await RegisterAndLoadCommandsAsync(wrappers, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Loads external (out-of-process WinRT) command providers. Call this after
    /// the root page is displayed so the UI is not blocked by extension startup.
    /// Commands appear progressively via <see cref="IExtensionService.OnProviderAdded"/>.
    /// </summary>
    [RelayCommand]
    public async Task LoadExternalProvidersAsync()
    {
        IsLoading = true;
        try
        {
            var ct = _currentExtensionLoadCancellationToken;
            foreach (var service in _extensionServices.Where(s => s is not BuiltInExtensionService))
            {
                var wrappers = await service.LoadProvidersAsync(ct).ConfigureAwait(false);
                await RegisterAndLoadCommandsAsync(wrappers, ct).ConfigureAwait(false);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ReloadAllCommandsAsyncCore(CancellationToken cancellationToken)
    {
        IsLoading = true;

        try
        {
            // Invalidate any background continuations from the previous load cycle
            await _extensionLoadCts.CancelAsync().ConfigureAwait(false);
            _extensionLoadCts.Dispose();
            _extensionLoadCts = new();
            _currentExtensionLoadCancellationToken = _extensionLoadCts.Token;

            // Signal all services to stop their running providers
            foreach (var service in _extensionServices)
            {
                await service.SignalStopAsync().ConfigureAwait(false);
            }

            lock (TopLevelCommands)
            {
                TopLevelCommands.Clear();
            }

            lock (_dockBandsLock)
            {
                DockBands.Clear();
            }

            lock (_commandProvidersLock)
            {
                _commandProviders.Clear();
            }

            var ct = _currentExtensionLoadCancellationToken;

            // Load providers from each service sequentially (order matters: built-ins first)
            foreach (var service in _extensionServices)
            {
                var wrappers = await service.LoadProvidersAsync(ct).ConfigureAwait(false);
                await RegisterAndLoadCommandsAsync(wrappers, ct).ConfigureAwait(false);
            }
        }
        finally
        {
            IsLoading = false;
            WeakReferenceMessenger.Default.Send<ReloadFinishedMessage>();
        }
    }

    private async Task UpdateProviderEnabledStateAsyncCore(string providerId, bool isEnabled)
    {
        IsLoading = true;

        try
        {
            // If disabled, we'll remove that providers commands from top level commands, dock bands, and pinned commands.
            if (!isEnabled)
            {
                lock (TopLevelCommands)
                {
                    var commandsToRemove = TopLevelCommands.Where(c => c.CommandProviderId == providerId).ToList();
                    foreach (var command in commandsToRemove)
                    {
                        TopLevelCommands.Remove(command);
                    }
                }

                lock (_dockBandsLock)
                {
                    var dockBandsToRemove = DockBands.Where(b => b.CommandProviderId == providerId).ToList();
                    foreach (var band in dockBandsToRemove)
                    {
                        DockBands.Remove(band);
                    }
                }

                lock (PinnedCommands)
                {
                    var pinnedToRemove = PinnedCommands.Where(p => p.ProviderId == providerId).ToList();
                    foreach (var command in pinnedToRemove)
                    {
                        PinnedCommands.Remove(command);
                    }
                }
            }
            else
            {
                CommandProviderWrapper? provider;
                lock (_commandProvidersLock)
                {
                    provider = _commandProviders.FirstOrDefault(p => p.ProviderId == providerId);
                }

                if (provider != null)
                {
                    await provider.LoadTopLevelCommands(_serviceProvider);

                    lock (TopLevelCommands)
                    {
                        foreach (var command in provider.TopLevelItems)
                        {
                            if (!TopLevelCommands.Any(a => a.Id == command.Id))
                            {
                                TopLevelCommands.Add(command);
                            }
                        }

                        foreach (var item in provider.FallbackItems)
                        {
                            if (!TopLevelCommands.Any(a => a.Id == item.Id) && item.IsEnabled)
                            {
                                TopLevelCommands.Add(item);
                            }
                        }
                    }

                    lock (_dockBandsLock)
                    {
                        foreach (var band in provider.DockBandItems)
                        {
                            if (!DockBands.Any(a => a.Id == band.Id))
                            {
                                DockBands.Add(band);
                            }
                        }
                    }
                }
                else
                {
                    Logger.LogWarning($"Could not find provider with id '{providerId}' to update enabled state.");
                    return;
                }
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task<RegisterAndLoadSummary> RegisterAndLoadCommandsAsync(IEnumerable<CommandProviderWrapper> wrappers, CancellationToken ct)
    {
        var wrapperList = wrappers.ToList();
        lock (_commandProvidersLock)
        {
            _commandProviders.AddRange(wrapperList);
        }

        // Load the commands from the providers in parallel
        var loadResults = await Task.WhenAll(wrapperList.Select(w => TryLoadCommandsAsync(w, ct))).ConfigureAwait(false);

        var totalCommands = 0;
        var totalDockBands = 0;
        var timedOut = new List<CommandLoadResult>();
        List<TopLevelViewModel> commandsToAdd = [];
        List<TopLevelViewModel> dockBandsToAdd = [];

        foreach (var r in loadResults)
        {
            if (r.IsLoaded)
            {
                var commands = r.TopLevelObjectSets.Commands;
                if (commands is not null)
                {
                    foreach (var c in commands)
                    {
                        commandsToAdd.Add(c);
                        totalCommands++;
                    }
                }

                var bands = r.TopLevelObjectSets.DockBands;
                if (bands is not null)
                {
                    foreach (var b in bands)
                    {
                        dockBandsToAdd.Add(b);
                        totalDockBands++;
                    }
                }
            }
            else if (r.IsTimedOut)
            {
                timedOut.Add(r);
            }
        }

        lock (TopLevelCommands)
        {
            foreach (var c in commandsToAdd)
            {
                TopLevelCommands.Add(c);
            }
        }

        lock (_dockBandsLock)
        {
            foreach (var b in dockBandsToAdd)
            {
                DockBands.Add(b);
            }
        }

        // Fire background continuations for timed-out loads outside the lock
        foreach (var r in timedOut)
        {
            if (r.IsTimedOut)
            {
                _ = AppendCommandsWhenReadyAsync(r.Wrapper, r.PendingLoadTask, r.Stopwatch, ct);
            }
        }

        return new RegisterAndLoadSummary(totalCommands, totalDockBands);
    }

    private async Task<CommandLoadResult> TryLoadCommandsAsync(CommandProviderWrapper wrapper, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var loadTask = LoadTopLevelCommandsFromProvider(wrapper);
        try
        {
            var result = await loadTask.WaitAsync(CommandLoadTimeout, ct).ConfigureAwait(false);
            var commandCount = result.Commands?.Count ?? 0;
            var dockBandCount = result.DockBands?.Count ?? 0;
            Logger.LogInfo($"Loaded {commandCount} command(s) and {dockBandCount} band(s) from {wrapper.ExtensionHost?.Extension?.PackageFullName ?? wrapper.DisplayName} in {sw.ElapsedMilliseconds} ms");
            return CommandLoadResult.Loaded(wrapper, result);
        }
        catch (TimeoutException)
        {
            Logger.LogWarning($"Loading commands and bands from {wrapper.ExtensionHost?.Extension?.PackageFullName ?? wrapper.DisplayName} timed out after {sw.ElapsedMilliseconds} ms, continuing in background");
            return CommandLoadResult.TimedOut(wrapper, loadTask, sw);
        }
        catch (OperationCanceledException)
        {
            Logger.LogDebug($"Loading commands and bands from {wrapper.ExtensionHost?.Extension?.PackageFullName ?? wrapper.DisplayName} was cancelled after {sw.ElapsedMilliseconds} ms");
            return CommandLoadResult.Failed(wrapper);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to load commands and bands for {wrapper.ExtensionHost?.Extension?.PackageFullName ?? wrapper.DisplayName} after {sw.ElapsedMilliseconds} ms: {ex}");
            return CommandLoadResult.Failed(wrapper);
        }
    }

    private async Task AppendCommandsWhenReadyAsync(
        CommandProviderWrapper wrapper,
        Task<TopLevelObjectSets> loadTask,
        Stopwatch sw,
        CancellationToken ct)
    {
        try
        {
            var topLevelObjectSets = await loadTask.WaitAsync(BackgroundCommandLoadTimeout, ct).ConfigureAwait(false);

            var commands = topLevelObjectSets.Commands;
            if (commands is not null)
            {
                lock (TopLevelCommands)
                {
                    foreach (var c in commands)
                    {
                        TopLevelCommands.Add(c);
                    }
                }
            }

            var dockBands = topLevelObjectSets.DockBands;
            if (dockBands is not null)
            {
                lock (_dockBandsLock)
                {
                    foreach (var band in dockBands)
                    {
                        DockBands.Add(band);
                    }
                }
            }

            Logger.LogInfo($"Late-loaded {commands?.Count ?? 0} command(s) and {dockBands?.Count ?? 0} band(s) from {wrapper.ExtensionHost?.Extension?.PackageFullName ?? wrapper.DisplayName} in {sw.ElapsedMilliseconds} ms");
        }
        catch (OperationCanceledException)
        {
            // Reload happened - discard stale results
        }
        catch (Exception ex)
        {
            Logger.LogError($"Background loading of commands and bands from {wrapper.ExtensionHost?.Extension?.PackageFullName ?? wrapper.DisplayName} failed after {sw.ElapsedMilliseconds} ms: {ex}");
        }
    }

    private void ExtensionService_OnProviderAdded(IExtensionService sender, IEnumerable<CommandProviderWrapper> wrappers)
    {
        var ct = _currentExtensionLoadCancellationToken;

        _ = Task.Run(
            async () =>
            {
                await RegisterAndLoadCommandsAsync(wrappers, ct).ConfigureAwait(false);
            },
            ct);
    }

    private void ExtensionService_OnProviderRemoved(IExtensionService sender, IEnumerable<CommandProviderWrapper> removedWrappers)
    {
        // When we get a provider removal event, hop off to a BG thread
        _ = Task.Run(
            async () =>
            {
                var removedProviderIds = new HashSet<string>(removedWrappers.Select(w => w.ProviderId));

                List<TopLevelViewModel> commandsToRemove = [];
                List<TopLevelViewModel> bandsToRemove = [];

                lock (TopLevelCommands)
                {
                    foreach (var command in TopLevelCommands)
                    {
                        if (removedProviderIds.Contains(command.CommandProviderId))
                        {
                            commandsToRemove.Add(command);
                        }
                    }
                }

                lock (_dockBandsLock)
                {
                    foreach (var band in DockBands)
                    {
                        if (removedProviderIds.Contains(band.CommandProviderId))
                        {
                            bandsToRemove.Add(band);
                        }
                    }
                }

                lock (_commandProvidersLock)
                {
                    _commandProviders.RemoveAll(w => removedProviderIds.Contains(w.ProviderId));
                }

                await Task.Factory.StartNew(
                () =>
                {
                    lock (TopLevelCommands)
                    {
                        if (commandsToRemove.Count != 0)
                        {
                            foreach (var deleted in commandsToRemove)
                            {
                                TopLevelCommands.Remove(deleted);
                            }
                        }
                    }

                    lock (_dockBandsLock)
                    {
                        if (bandsToRemove.Count != 0)
                        {
                            foreach (var deleted in bandsToRemove)
                            {
                                DockBands.Remove(deleted);
                            }
                        }
                    }
                },
                CancellationToken.None,
                TaskCreationOptions.None,
                _taskScheduler);
            });
    }

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
            foreach (var command in DockBands)
            {
                if (command.Id == id)
                {
                    return command;
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

    public void Receive(ReloadCommandsMessage message) =>
        _ = ReloadAllCommandsAsync();

    public void Receive(ProviderEnabledStateChangedMessage message) =>
        _ = UpdateProviderEnabledStateAsyncCore(message.ProviderId, message.IsEnabled);

    public void Receive(PinCommandItemMessage message)
    {
        var wrapper = LookupProvider(message.ProviderId);
        wrapper?.PinCommand(message.CommandId, _serviceProvider);
        RebuildPinnedCache();
    }

    public void Receive(UnpinCommandItemMessage message)
    {
        var wrapper = LookupProvider(message.ProviderId);
        wrapper?.UnpinCommand(message.CommandId, _serviceProvider);
        RebuildPinnedCache();
    }

    public void Receive(PinToDockMessage message)
    {
        if (LookupProvider(message.ProviderId) is CommandProviderWrapper wrapper)
        {
            if (message.Pin)
            {
                wrapper?.PinDockBand(message.CommandId, _serviceProvider, message.WithReload, message.Side, message.ShowTitles, message.ShowSubtitles, message.MonitorDeviceId);
            }
            else
            {
                wrapper?.UnpinDockBand(message.CommandId, _serviceProvider, message.WithReload);
            }
        }
        else
        {
            Logger.LogWarning($"[DockDrop] PinToDockMessage: no provider found for '{message.ProviderId}'");
        }
    }

    public CommandProviderWrapper? LookupProvider(string providerId)
    {
        lock (_commandProvidersLock)
        {
            return _commandProviders.FirstOrDefault(w => w.ProviderId == providerId);
        }
    }

    internal bool IsProviderActive(string id)
    {
        lock (_commandProvidersLock)
        {
            return _commandProviders.Any(wrapper => wrapper.Id == id && wrapper.IsActive);
        }
    }

    internal void PinDockBand(TopLevelViewModel bandVm)
    {
        lock (_dockBandsLock)
        {
            foreach (var existing in DockBands)
            {
                if (existing.Id == bandVm.Id)
                {
                    // already pinned
                    Logger.LogDebug($"Dock band '{bandVm.Id}' is already pinned.");
                    return;
                }
            }

            Logger.LogDebug($"Attempting to pin dock band '{bandVm.Id}' from provider '{bandVm.CommandProviderId}'.");
            var providerId = bandVm.CommandProviderId;
            var foundProvider = false;

            // WATCH OUT: This locks CommandProviders. If you add code that
            // locks CommandProviders first, before locking DockBands, you will
            // cause a deadlock.
            foreach (var provider in CommandProviders)
            {
                if (provider.Id == providerId)
                {
                    Logger.LogDebug($"Found provider '{providerId}' to pin dock band '{bandVm.Id}'.");
                    provider.PinDockBand(bandVm);
                    foundProvider = true;
                    break;
                }
            }

            if (!foundProvider)
            {
                Logger.LogWarning($"Could not find provider '{providerId}' to pin dock band '{bandVm.Id}'.");
            }
            else
            {
                // Add the band to DockBands if not already present
                if (!DockBands.Any(b => b.Id == bandVm.Id))
                {
                    DockBands.Add(bandVm);
                }
            }
        }
    }

    public void Dispose()
    {
        foreach (var service in _extensionServices)
        {
            service.OnProviderAdded -= ExtensionService_OnProviderAdded;
            service.OnProviderRemoved -= ExtensionService_OnProviderRemoved;
        }

        _extensionLoadCts.Cancel();
        _extensionLoadCts.Dispose();
        _reloadCommandsGate.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed class CommandLoadResult
    {
        public TopLevelObjectSets? TopLevelObjectSets { get; private init; }

        public CommandProviderWrapper Wrapper { get; }

        public Task<TopLevelObjectSets>? PendingLoadTask { get; private init; }

        public Stopwatch? Stopwatch { get; private init; }

        [MemberNotNullWhen(true, nameof(TopLevelObjectSets))]
        public bool IsLoaded => TopLevelObjectSets is not null;

        [MemberNotNullWhen(true, nameof(PendingLoadTask), nameof(Stopwatch))]
        public bool IsTimedOut => PendingLoadTask is not null;

        private CommandLoadResult(CommandProviderWrapper wrapper)
        {
            Wrapper = wrapper;
        }

        public static CommandLoadResult Loaded(CommandProviderWrapper wrapper, TopLevelObjectSets topLevelObjectSets)
        {
            return new CommandLoadResult(wrapper) { TopLevelObjectSets = topLevelObjectSets };
        }

        public static CommandLoadResult TimedOut(CommandProviderWrapper wrapper, Task<TopLevelObjectSets> pendingLoadTask, Stopwatch sw)
        {
            return new CommandLoadResult(wrapper) { PendingLoadTask = pendingLoadTask, Stopwatch = sw };
        }

        public static CommandLoadResult Failed(CommandProviderWrapper wrapper)
        {
            return new CommandLoadResult(wrapper);
        }
    }

    private readonly record struct RegisterAndLoadSummary(int CommandCount, int DockBandCount);

    private record TopLevelObjectSets(ICollection<TopLevelViewModel>? Commands, ICollection<TopLevelViewModel>? DockBands);
}
