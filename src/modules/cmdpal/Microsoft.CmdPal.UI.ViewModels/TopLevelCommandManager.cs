// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.ComponentModel;
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
    IDisposable,
    IAsyncDisposable
{
    private static readonly TimeSpan CommandLoadTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan BackgroundCommandLoadTimeout = TimeSpan.FromSeconds(60);

    private readonly IServiceProvider _serviceProvider;
    private readonly IEnumerable<IExtensionService> _extensionServices;
    private readonly TaskScheduler _taskScheduler;
    private readonly TimeProvider _timeProvider;

    private readonly List<CommandProviderWrapper> _commandProviders = [];
    private readonly Lock _commandProvidersLock = new();
    private readonly Lock _pinnedCommandsUpdateLock = new();

    // watch out: if you add code that locks CommandProviders, be sure to always
    // lock CommandProviders before locking DockBands, or you will cause a
    // deadlock.
    private readonly Lock _dockBandsLock = new();
    private readonly CoalescingAsyncRunner _reloadCommandsRunner;
    private readonly Lock _stateLock = new();
    private TaskCompletionSource? _disposeCompletion;
    private TaskCompletionSource? _providerActivitiesDrained;
    private bool _disposing;
    private int _providerActivities;
    private int _providerActivitiesEnding;
    private bool _startupPending = true;
    private bool _isLoading = true;
    private CancellationTokenSource _extensionLoadCts = new();
    private CancellationToken _currentExtensionLoadCancellationToken;
    private IReadOnlyList<PinnedCommandSettings> _pinnedCommands = [];

    internal event EventHandler? PinnedCommandsChanged;

    public TopLevelCommandManager(IServiceProvider serviceProvider, IEnumerable<IExtensionService> extensionServices)
        : this(serviceProvider, extensionServices, TimeProvider.System)
    {
    }

    internal TopLevelCommandManager(IServiceProvider serviceProvider, IEnumerable<IExtensionService> extensionServices, TimeProvider timeProvider)
    {
        _serviceProvider = serviceProvider;
        _extensionServices = extensionServices;
        _timeProvider = timeProvider;
        _currentExtensionLoadCancellationToken = _extensionLoadCts.Token;
        _taskScheduler = _serviceProvider.GetService<TaskScheduler>()!;
        WeakReferenceMessenger.Default.Register<ReloadCommandsMessage>(this);
        WeakReferenceMessenger.Default.Register<ProviderEnabledStateChangedMessage>(this);
        WeakReferenceMessenger.Default.Register<PinCommandItemMessage>(this);
        WeakReferenceMessenger.Default.Register<UnpinCommandItemMessage>(this);
        WeakReferenceMessenger.Default.Register<PinToDockMessage>(this);
        _reloadCommandsRunner = new(ReloadAllCommandsAsyncCore);
        _reloadCommandsRunner.BusyChanged += ReloadBusyChanged;
        RebuildPinnedCache();

        foreach (var service in _extensionServices)
        {
            service.OnProviderAdded += ExtensionService_OnProviderAdded;
            service.OnProviderRemoved += ExtensionService_OnProviderRemoved;
        }
    }

    public ObservableCollection<TopLevelViewModel> TopLevelCommands { get; set; } = [];

    // DockBands uses a custom collection so that bulk changes (see
    // UpdateCommandsForProvider, RegisterAndLoadCommandsAsync) raise a single
    // Reset notification instead of one event per inserted/removed/moved item. The dock subscribes to this
    // collection and does a full rebuild per event, so collapsing the burst
    // here avoids dozens of redundant rebuilds for one provider reload.
    public DockBandsCollection DockBands { get; } = new();

    /// <summary>
    /// Gets a value indicating whether startup, a reload, or a provider activity is in progress.
    /// </summary>
    public bool IsLoading
    {
        get
        {
            lock (_stateLock)
            {
                return _isLoading;
            }
        }
    }

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

    internal IReadOnlyList<PinnedCommandSettings> GetPinnedCommandsSnapshot()
    {
        return Volatile.Read(ref _pinnedCommands);
    }

    internal void RebuildPinnedCache()
    {
        bool changed;
        var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();

        lock (_pinnedCommandsUpdateLock)
        {
            var pinnedCommands = settingsService.Settings.PinnedCommands;
            changed = !_pinnedCommands.SequenceEqual(pinnedCommands);
            Volatile.Write(ref _pinnedCommands, pinnedCommands);
        }

        if (changed)
        {
            PinnedCommandsChanged?.Invoke(this, EventArgs.Empty);
        }
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
        List<TopLevelViewModel> removedTopLevel;
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

            ListHelpers.InPlaceUpdateList(TopLevelCommands, clone, out removedTopLevel);
        }

        List<TopLevelViewModel> removedDockBands;
        lock (_dockBandsLock)
        {
            // Same idea as TopLevelCommands above, but we deliberately use
            // ReplaceWith so the dock only sees one CollectionChanged event
            // for the whole rewrite instead of one per item.
            List<TopLevelViewModel> dockClone = [.. DockBands];
            var dockStartIndex = FindIndexForFirstProviderItem(dockClone, sender.ProviderId);
            dockClone.RemoveAll(item => item.CommandProviderId == sender.ProviderId);
            dockClone.InsertRange(dockStartIndex, newBands);

            // ReplaceWith doesn't report what it dropped, so diff the current
            // contents against the new ones before we overwrite them. Bands
            // that get re-inserted from newBands are correctly not reported.
            removedDockBands = [.. DockBands.Except(dockClone)];

            DockBands.ReplaceWith(dockClone);
        }

        foreach (var item in removedTopLevel)
        {
            item.Cleanup();
        }

        foreach (var item in removedDockBands)
        {
            item.Cleanup();
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

    /// <summary>
    /// Requests a command reload, coalescing requests that arrive before the next run starts.
    /// </summary>
    /// <returns>A task with the assigned reload's outcome, or a canceled task if disposal has started.</returns>
    public Task ReloadAllCommandsAsync()
    {
        Task request;
        bool loadingChanged;
        lock (_stateLock)
        {
            request = _reloadCommandsRunner.RequestAsync();
            loadingChanged = UpdateLoadingState();
        }

        NotifyLoadingChanged(loadingChanged);
        return request;
    }

    private void ReloadBusyChanged(bool isBusy)
    {
        bool loadingChanged;
        lock (_stateLock)
        {
            loadingChanged = UpdateLoadingState();
        }

        NotifyLoadingChanged(loadingChanged);

        bool reloadFinished;
        lock (_stateLock)
        {
            // A loading observer may have requested another reload
            reloadFinished = !isBusy && !_reloadCommandsRunner.IsBusy && !_disposing;
        }

        if (reloadFinished)
        {
            WeakReferenceMessenger.Default.Send<ReloadFinishedMessage>();
        }
    }

    private bool BeginProviderActivity(out CancellationToken cancellationToken, out bool loadingChanged)
    {
        cancellationToken = default;
        loadingChanged = false;
        lock (_stateLock)
        {
            if (_disposing)
            {
                return false;
            }

            if (_providerActivities == 0 && _providerActivitiesEnding == 0)
            {
                _providerActivitiesDrained = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            _providerActivities++;
            cancellationToken = _currentExtensionLoadCancellationToken;
            loadingChanged = UpdateLoadingState();
            return true;
        }
    }

    private void EndProviderActivity(bool completesStartup = false)
    {
        bool loadingChanged;
        lock (_stateLock)
        {
            _providerActivities--;
            _providerActivitiesEnding++;
            if (completesStartup)
            {
                _startupPending = false;
            }

            loadingChanged = UpdateLoadingState();
        }

        try
        {
            NotifyLoadingChanged(loadingChanged);
        }
        finally
        {
            lock (_stateLock)
            {
                // Disposal also waits for the final loading notifications
                _providerActivitiesEnding--;
                if (_providerActivities == 0 && _providerActivitiesEnding == 0)
                {
                    _providerActivitiesDrained!.TrySetResult();
                }
            }
        }
    }

    private void CompleteStartup()
    {
        bool loadingChanged;
        lock (_stateLock)
        {
            _startupPending = false;
            loadingChanged = UpdateLoadingState();
        }

        NotifyLoadingChanged(loadingChanged);
    }

    private bool UpdateLoadingState()
    {
        // The caller holds _stateLock while updating the loading inputs
        var isLoading = _startupPending || _providerActivities != 0 || _reloadCommandsRunner.IsBusy;
        if (_isLoading == isLoading)
        {
            return false;
        }

        _isLoading = isLoading;
        return true;
    }

    private void NotifyLoadingChanged(bool loadingChanged)
    {
        if (loadingChanged)
        {
            try
            {
                OnPropertyChanged(nameof(IsLoading));
            }
            catch (Exception ex)
            {
                try
                {
                    Logger.LogError("Failed to notify the command loading state", ex);
                }
                catch (Exception)
                {
                    // Notification and logging failures must not change accepted work's outcome
                }
            }
        }
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
    // Startup calls Execute on a worker; keep cancellation and failures on ExecutionTask to avoid a fatal rethrow
    [RelayCommand(FlowExceptionsToTaskScheduler = true)]
    public async Task LoadExternalProvidersAsync()
    {
        if (!BeginProviderActivity(out var ct, out var loadingChanged))
        {
            return;
        }

        try
        {
            NotifyLoadingChanged(loadingChanged);
            foreach (var service in _extensionServices.Where(s => s is not BuiltInExtensionService))
            {
                ct.ThrowIfCancellationRequested();

                // Keep the activity alive until the service stops using its dependencies
                var wrappers = await service.LoadProvidersAsync(ct).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();
                await RegisterAndLoadCommandsAsync(wrappers, ct).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.LogError("Failed to load external command providers", ex);
            throw;
        }
        finally
        {
            EndProviderActivity(completesStartup: true);
        }
    }

    private async Task ReloadAllCommandsAsyncCore()
    {
        try
        {
            CancellationTokenSource previousLoad;
            Task cancellation;
            lock (_stateLock)
            {
                if (_disposing)
                {
                    throw new OperationCanceledException(_currentExtensionLoadCancellationToken);
                }

                previousLoad = _extensionLoadCts;
                cancellation = previousLoad.CancelAsync();
            }

            CancellationToken ct;
            try
            {
                await cancellation.ConfigureAwait(false);
            }
            finally
            {
                // A throwing callback must not leave later activities on the canceled generation
                bool replaceGeneration;
                lock (_stateLock)
                {
                    replaceGeneration = !_disposing;
                    if (replaceGeneration)
                    {
                        _extensionLoadCts = new();
                        _currentExtensionLoadCancellationToken = _extensionLoadCts.Token;
                    }

                    ct = _currentExtensionLoadCancellationToken;
                }

                if (replaceGeneration)
                {
                    previousLoad.Dispose();
                }
            }

            // Signal all services to stop their running providers
            foreach (var service in _extensionServices)
            {
                ct.ThrowIfCancellationRequested();
                await service.SignalStopAsync().ConfigureAwait(false);
            }

            ct.ThrowIfCancellationRequested();
            List<TopLevelViewModel> removedTopLevel;
            lock (TopLevelCommands)
            {
                removedTopLevel = [.. TopLevelCommands];
                TopLevelCommands.Clear();
            }

            List<TopLevelViewModel> removedDockBands;
            lock (_dockBandsLock)
            {
                removedDockBands = [.. DockBands];
                DockBands.Clear();
            }

            lock (_commandProvidersLock)
            {
                _commandProviders.Clear();
            }

            foreach (var item in removedTopLevel)
            {
                item.Cleanup();
            }

            foreach (var item in removedDockBands)
            {
                item.Cleanup();
            }

            // Load providers from each service sequentially (order matters: built-ins first)
            foreach (var service in _extensionServices)
            {
                ct.ThrowIfCancellationRequested();
                var wrappers = await service.LoadProvidersAsync(ct).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();
                await RegisterAndLoadCommandsAsync(wrappers, ct).ConfigureAwait(false);
            }

            ct.ThrowIfCancellationRequested();
        }
        finally
        {
            CompleteStartup();
        }
    }

    private async Task UpdateProviderEnabledStateAsyncCore(string providerId, bool isEnabled)
    {
        if (!BeginProviderActivity(out var ct, out var loadingChanged))
        {
            return;
        }

        try
        {
            NotifyLoadingChanged(loadingChanged);
            ct.ThrowIfCancellationRequested();

            CommandProviderWrapper? provider;
            lock (_commandProvidersLock)
            {
                provider = _commandProviders.FirstOrDefault(p => p.ProviderId == providerId);
            }

            // If disabled, we'll remove that providers commands from top level commands, dock bands, and pinned commands.
            if (!isEnabled)
            {
                provider?.InvalidatePendingCommands();
                List<TopLevelViewModel> commandsToRemove;
                lock (TopLevelCommands)
                {
                    commandsToRemove = TopLevelCommands.Where(c => c.CommandProviderId == providerId).ToList();
                    foreach (var command in commandsToRemove)
                    {
                        TopLevelCommands.Remove(command);
                    }
                }

                List<TopLevelViewModel> dockBandsToRemove;
                lock (_dockBandsLock)
                {
                    dockBandsToRemove = DockBands.Where(b => b.CommandProviderId == providerId).ToList();
                    DockBands.RemoveRange(dockBandsToRemove);
                }

                // Re-enabling the provider reloads its commands, which builds
                // brand new view-models, so the ones we just dropped are never
                // coming back and can release their extension event handlers.
                foreach (var command in commandsToRemove)
                {
                    command.Cleanup();
                }

                foreach (var band in dockBandsToRemove)
                {
                    band.Cleanup();
                }
            }
            else
            {
                if (provider != null)
                {
                    var loadTask = provider.PrepareTopLevelCommandsAsync(_serviceProvider);
                    CommandProviderWrapper.PreparedCommands prepared;
                    try
                    {
                        // Drain canceled enables too, but never let a hung extension hold loading forever
                        prepared = await loadTask.WaitAsync(CommandLoadTimeout, _timeProvider).ConfigureAwait(false);
                    }
                    catch (TimeoutException)
                    {
                        _ = CompleteTimedOutEnableAsync(provider, loadTask, ct);
                        Logger.LogWarning($"Timed out loading commands while enabling provider '{providerId}'.");
                        return;
                    }

                    PublishEnabledCommands(provider, prepared, ct);
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
            EndProviderActivity();
        }
    }

    private async Task CompleteTimedOutEnableAsync(
        CommandProviderWrapper provider,
        Task<CommandProviderWrapper.PreparedCommands> loadTask,
        CancellationToken ct)
    {
        try
        {
            var prepared = await loadTask.ConfigureAwait(false);

            if (ct.IsCancellationRequested || !provider.IsCurrentLoad(prepared))
            {
                prepared.Cleanup();
                return;
            }

            // Only publication participates in loading and disposal after the foreground wait expires
            if (!BeginProviderActivity(out _, out var loadingChanged))
            {
                prepared.Cleanup();
                return;
            }

            try
            {
                NotifyLoadingChanged(loadingChanged);
                PublishEnabledCommands(provider, prepared, ct);
            }
            finally
            {
                EndProviderActivity();
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to complete a timed-out enable for provider '{provider.ProviderId}'.", ex);
        }
    }

    private void PublishEnabledCommands(CommandProviderWrapper provider, CommandProviderWrapper.PreparedCommands prepared, CancellationToken ct)
    {
        bool isRegistered;
        lock (_commandProvidersLock)
        {
            isRegistered = _commandProviders.Contains(provider);
        }

        if (ct.IsCancellationRequested || !isRegistered || !provider.TryPublishTopLevelCommands(prepared, _serviceProvider))
        {
            prepared.Cleanup();
            ct.ThrowIfCancellationRequested();
            return;
        }

        lock (TopLevelCommands)
        {
            foreach (var command in prepared.TopLevelItems ?? [])
            {
                if (!ct.IsCancellationRequested && provider.IsCurrentLoad(prepared) && !TopLevelCommands.Any(a => a.Id == command.Id))
                {
                    TopLevelCommands.Add(command);
                }
            }

            foreach (var item in prepared.FallbackItems ?? [])
            {
                if (!ct.IsCancellationRequested && provider.IsCurrentLoad(prepared) && !TopLevelCommands.Any(a => a.Id == item.Id) && item.IsEnabled)
                {
                    TopLevelCommands.Add(item);
                }
            }
        }

        lock (_dockBandsLock)
        {
            if (!ct.IsCancellationRequested && provider.IsCurrentLoad(prepared))
            {
                var existingIds = DockBands.Select(b => b.Id).ToHashSet();
                DockBands.AddRange((prepared.DockBandItems ?? []).Where(band => existingIds.Add(band.Id)));
            }
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

        // One notification for the whole batch; the dock rebuilds once per
        // DockBands change, so per-item adds would rebuild it once per band.
        lock (_dockBandsLock)
        {
            DockBands.AddRange(dockBandsToAdd);
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
                    DockBands.AddRange(dockBands);
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
                        DockBands.RemoveRange(bandsToRemove);
                    }

                    foreach (var deleted in commandsToRemove)
                    {
                        deleted.Cleanup();
                    }

                    foreach (var deleted in bandsToRemove)
                    {
                        deleted.Cleanup();
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

    public TopLevelViewModel? LookupCommand(string providerId, string commandId)
    {
        lock (TopLevelCommands)
        {
            foreach (var command in TopLevelCommands)
            {
                if (!command.IsFallback &&
                    string.Equals(command.CommandProviderId, providerId, StringComparison.Ordinal) &&
                    string.Equals(command.Id, commandId, StringComparison.Ordinal))
                {
                    return command;
                }
            }
        }

        return null;
    }

    public bool IsProviderEnabled(string providerId)
    {
        // A loaded wrapper can retain IsActive after its provider is disabled.
        var settings = _serviceProvider.GetRequiredService<ISettingsService>().Settings;
        return !settings.ProviderSettings.TryGetValue(providerId, out var providerSettings) || providerSettings.IsEnabled;
    }

    public async Task<CommandResolution?> ResolveCommandAsync(
        string providerId,
        string commandId,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            if (attempt > 0)
            {
                await WaitForCurrentLoadAsync(cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var provider = LookupProvider(providerId);
            if (provider is null || !IsProviderEnabled(providerId))
            {
                return null;
            }

            // 1. Check if the command is already loaded in memory (TopLevelCommands or DockBands)
            var command = LookupCommand(providerId, commandId) ?? LookupDockBand(providerId, commandId);
            if (command is not null && ReferenceEquals(command.ProviderContext, provider))
            {
                return new(command, provider, ownsCommand: false);
            }

            // 2. If not found, ask the provider to resolve the command (may involve async loading)
            cancellationToken.ThrowIfCancellationRequested();
            var resolveCommandFromProviderTask = Task.Run(() => provider.ResolveCommandItem(commandId, _serviceProvider), cancellationToken);
            try
            {
                // Get the task from provider
                command = await resolveCommandFromProviderTask.WaitAsync(cancellationToken).ConfigureAwait(false);

                // The provider may have been disabled or replaced while we were waiting for the command to resolve
                var actualProviderNow = LookupProvider(providerId);
                var isProviderEnabled = IsProviderEnabled(providerId);
                if (!ReferenceEquals(actualProviderNow, provider) || !isProviderEnabled)
                {
                    if (command is not null)
                    {
                        _ = CleanupThreadPool.Queue(command.Cleanup, $"Stale command '{commandId}' from provider '{providerId}'");
                    }

                    if (!isProviderEnabled)
                    {
                        return null;
                    }

                    continue;
                }

                return command is null ? null : new(command, provider, ownsCommand: true);
            }
            catch (OperationCanceledException)
            {
                // Release a result that arrives after the caller stops waiting.
                _ = resolveCommandFromProviderTask.ContinueWith(
                    task =>
                    {
                        if (task.Status == TaskStatus.RanToCompletion)
                        {
                            if (task.Result is { } lateCommand)
                            {
                                _ = CleanupThreadPool.Queue(lateCommand.Cleanup, $"Late command '{commandId}' from provider '{providerId}'");
                            }
                        }
                        else if (task.IsFaulted)
                        {
                            _ = task.Exception;
                        }
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default);
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to resolve command {commandId} from provider {providerId}.", ex);
                return null;
            }
        }

        return null;
    }

    private TopLevelViewModel? LookupDockBand(string providerId, string commandId)
    {
        lock (_dockBandsLock)
        {
            return DockBands.FirstOrDefault(command =>
                string.Equals(command.CommandProviderId, providerId, StringComparison.Ordinal) &&
                string.Equals(command.Id, commandId, StringComparison.Ordinal));
        }
    }

    /// <summary>Waits for the command load active at call time, excluding late provider continuations.</summary>
    public async Task WaitForCurrentLoadAsync(CancellationToken cancellationToken = default)
    {
        if (!IsLoading)
        {
            return;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        PropertyChangedEventHandler? handler = null;
        handler = (_, args) =>
        {
            if (args.PropertyName == nameof(IsLoading) && !IsLoading)
            {
                completion.TrySetResult();
            }
        };

        PropertyChanged += handler;
        try
        {
            // IsLoading may have changed between the first check and subscribing.
            if (!IsLoading)
            {
                return;
            }

            await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            PropertyChanged -= handler;
        }
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

    public void Receive(ReloadCommandsMessage message)
    {
        _ = ReloadAllCommandsAsync();
    }

    public void Receive(ProviderEnabledStateChangedMessage message)
    {
        _ = UpdateProviderEnabledStateAsyncCore(message.ProviderId, message.IsEnabled);
    }

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
        _ = ObserveDisposalAsync();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Stops accepting work, cancels the current load generation, and drains reloads and provider activities.
    /// Command loads that have timed out may continue in the background.
    /// </remarks>
    public ValueTask DisposeAsync()
    {
        TaskCompletionSource completion;
        Task cancellation;
        Task providerActivities;
        lock (_stateLock)
        {
            if (_disposeCompletion is not null)
            {
                return new ValueTask(_disposeCompletion.Task);
            }

            _disposing = true;
            _disposeCompletion = completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _reloadCommandsRunner.Dispose();
            cancellation = _extensionLoadCts.CancelAsync();
            providerActivities = _providerActivitiesDrained?.Task ?? Task.CompletedTask;
        }

        GC.SuppressFinalize(this);
        _ = DisposeCoreAsync(cancellation, providerActivities, completion);
        return new ValueTask(completion.Task);
    }

    private async Task ObserveDisposalAsync()
    {
        try
        {
            await DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to dispose the top-level command manager", ex);
        }
    }

    private async Task DisposeCoreAsync(Task cancellation, Task providerActivities, TaskCompletionSource completion)
    {
        try
        {
            try
            {
                _reloadCommandsRunner.BusyChanged -= ReloadBusyChanged;
                WeakReferenceMessenger.Default.UnregisterAll(this);
                foreach (var service in _extensionServices)
                {
                    service.OnProviderAdded -= ExtensionService_OnProviderAdded;
                    service.OnProviderRemoved -= ExtensionService_OnProviderRemoved;
                }
            }
            finally
            {
                try
                {
                    await Task.WhenAll(_reloadCommandsRunner.DisposeAsync().AsTask(), providerActivities, cancellation).ConfigureAwait(false);
                }
                finally
                {
                    _extensionLoadCts.Dispose();
                    CompleteStartup();
                }
            }

            completion.TrySetResult();
        }
        catch (Exception ex)
        {
            completion.TrySetException(ex);
        }
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
