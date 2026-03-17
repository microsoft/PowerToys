// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed partial class TopLevelCommandManager : ObservableObject,
    IRecipient<ReloadCommandsMessage>,
    IRecipient<PinCommandItemMessage>,
    IRecipient<UnpinCommandItemMessage>,
    IRecipient<PinToDockMessage>,
    IDisposable
{
    private static readonly TimeSpan ExtensionStartTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan CommandLoadTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan BackgroundStartTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan BackgroundCommandLoadTimeout = TimeSpan.FromSeconds(60);

    private readonly IServiceProvider _serviceProvider;
    private readonly ICommandProviderCache _commandProviderCache;
    private readonly TaskScheduler _taskScheduler;

    private readonly List<CommandProviderWrapper> _builtInCommands = [];
    private readonly List<CommandProviderWrapper> _extensionCommandProviders = [];
    private readonly Lock _commandProvidersLock = new();

    // watch out: if you add code that locks CommandProviders, be sure to always
    // lock CommandProviders before locking DockBands, or you will cause a
    // deadlock.
    private readonly Lock _dockBandsLock = new();
    private readonly SupersedingAsyncGate _reloadCommandsGate;
    private CancellationTokenSource _extensionLoadCts = new();
    private CancellationToken _currentExtensionLoadCancellationToken;

    public TopLevelCommandManager(IServiceProvider serviceProvider, ICommandProviderCache commandProviderCache)
    {
        _serviceProvider = serviceProvider;
        _commandProviderCache = commandProviderCache;
        _currentExtensionLoadCancellationToken = _extensionLoadCts.Token;
        _taskScheduler = _serviceProvider.GetService<TaskScheduler>()!;
        WeakReferenceMessenger.Default.Register<ReloadCommandsMessage>(this);
        WeakReferenceMessenger.Default.Register<PinCommandItemMessage>(this);
        WeakReferenceMessenger.Default.Register<UnpinCommandItemMessage>(this);
        WeakReferenceMessenger.Default.Register<PinToDockMessage>(this);
        _reloadCommandsGate = new(ReloadAllCommandsAsyncCore);
    }

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
                return _builtInCommands.Concat(_extensionCommandProviders).ToList();
            }
        }
    }

    public async Task<bool> LoadBuiltinsAsync()
    {
        var s = new Stopwatch();
        s.Start();

        lock (_commandProvidersLock)
        {
            _builtInCommands.Clear();
        }

        // Load built-In commands first. These are all in-proc, and
        // owned by our ServiceProvider.
        var builtInCommands = _serviceProvider.GetServices<ICommandProvider>();
        foreach (var provider in builtInCommands)
        {
            CommandProviderWrapper wrapper = new(provider, _taskScheduler);
            lock (_commandProvidersLock)
            {
                _builtInCommands.Add(wrapper);
            }

            var objects = await LoadTopLevelCommandsFromProvider(wrapper);
            lock (TopLevelCommands)
            {
                if (objects.Commands is IEnumerable<TopLevelViewModel> commands)
                {
                    foreach (var c in commands)
                    {
                        TopLevelCommands.Add(c);
                    }
                }
            }

            lock (_dockBandsLock)
            {
                if (objects.DockBands is IEnumerable<TopLevelViewModel> bands)
                {
                    foreach (var c in bands)
                    {
                        DockBands.Add(c);
                    }
                }
            }
        }

        s.Stop();

        Logger.LogDebug($"Loading built-ins took {s.ElapsedMilliseconds}ms");

        return true;
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
    private void CommandProvider_CommandsChanged(CommandProviderWrapper sender, IItemsChangedEventArgs args) =>
        _ = Task.Run(async () => await UpdateCommandsForProvider(sender, args));

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
            // same idea for DockBands
            List<TopLevelViewModel> dockClone = [.. DockBands];
            var dockStartIndex = FindIndexForFirstProviderItem(dockClone, sender.ProviderId);
            dockClone.RemoveAll(item => item.CommandProviderId == sender.ProviderId);
            dockClone.InsertRange(dockStartIndex, newBands);
            ListHelpers.InPlaceUpdateList(DockBands, dockClone);
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
        // this should be superseded with a cancellable version.
        await _reloadCommandsGate.ExecuteAsync(CancellationToken.None);
    }

    private async Task ReloadAllCommandsAsyncCore(CancellationToken cancellationToken)
    {
        IsLoading = true;

        // Invalidate any background continuations from the previous load cycle
        await _extensionLoadCts.CancelAsync().ConfigureAwait(false);
        _extensionLoadCts.Dispose();
        _extensionLoadCts = new();
        _currentExtensionLoadCancellationToken = _extensionLoadCts.Token;

        var extensionService = _serviceProvider.GetService<IExtensionService>()!;
        await extensionService.SignalStopExtensionsAsync().ConfigureAwait(false);

        lock (TopLevelCommands)
        {
            TopLevelCommands.Clear();
        }

        lock (_dockBandsLock)
        {
            DockBands.Clear();
        }

        await LoadBuiltinsAsync().ConfigureAwait(false);
        _ = Task.Run(LoadExtensionsAsync, cancellationToken);
    }

    // Load commands from our extensions. Called on a background thread.
    // Currently, this
    // * queries the package catalog,
    // * starts all the extensions,
    // * then fetches the top-level commands from them.
    // TODO In the future, we'll probably abstract some of this away, to have
    // separate extension tracking vs stub loading.
    [RelayCommand]
    public async Task<bool> LoadExtensionsAsync()
    {
        var extensionService = _serviceProvider.GetService<IExtensionService>()!;

        extensionService.OnExtensionAdded -= ExtensionService_OnExtensionAdded;
        extensionService.OnExtensionRemoved -= ExtensionService_OnExtensionRemoved;

        var ct = _currentExtensionLoadCancellationToken;

        var extensions = (await extensionService.GetInstalledExtensionsAsync().ConfigureAwait(false)).ToImmutableList();
        lock (_commandProvidersLock)
        {
            _extensionCommandProviders.Clear();
        }

        await StartExtensionsAndGetCommands(extensions, ct).ConfigureAwait(false);

        extensionService.OnExtensionAdded += ExtensionService_OnExtensionAdded;
        extensionService.OnExtensionRemoved += ExtensionService_OnExtensionRemoved;

        IsLoading = false;

        // Send on the current thread; receivers should marshal to UI if needed
        WeakReferenceMessenger.Default.Send<ReloadFinishedMessage>();

        return true;
    }

    private void ExtensionService_OnExtensionAdded(IExtensionService sender, IEnumerable<IExtensionWrapper> extensions)
    {
        var ct = _currentExtensionLoadCancellationToken;

        // When we get an extension install event, hop off to a BG thread
        _ = Task.Run(
            async () =>
            {
                // for each newly installed extension, start it and get commands
                // from it. One single package might have more than one
                // IExtensionWrapper in it.
                await StartExtensionsAndGetCommands(extensions, ct).ConfigureAwait(false);
            },
            ct);
    }

    private async Task StartExtensionsAndGetCommands(IEnumerable<IExtensionWrapper> extensions, CancellationToken ct)
    {
        var timer = Stopwatch.StartNew();

        // Start all extensions in parallel
        var startResults = await Task.WhenAll(extensions.Select(TryStartExtensionAsync)).ConfigureAwait(false);

        var startedWrappers = new List<CommandProviderWrapper>();
        foreach (var r in startResults)
        {
            if (r.IsStarted)
            {
                startedWrappers.Add(r.Wrapper);
            }
            else if (r.IsTimedOut)
            {
                _ = StartExtensionWhenReadyAsync(r.Extension, r.PendingStartTask, r.Stopwatch, ct);
            }
        }

        // Register started extensions and load their commands
        var loadSummary = await RegisterAndLoadCommandsAsync(startedWrappers, ct).ConfigureAwait(false);

        timer.Stop();
        Logger.LogInfo($"Loaded {loadSummary.CommandCount} command(s) and {loadSummary.DockBandCount} band(s) from {startedWrappers.Count} extension(s) in {timer.ElapsedMilliseconds} ms");
    }

    private async Task<RegisterAndLoadSummary> RegisterAndLoadCommandsAsync(ICollection<CommandProviderWrapper> wrappers, CancellationToken ct)
    {
        lock (_commandProvidersLock)
        {
            _extensionCommandProviders.AddRange(wrappers);
        }

        // Load the commands from the providers in parallel
        var loadResults = await Task.WhenAll(wrappers.Select(w => TryLoadCommandsAsync(w, ct))).ConfigureAwait(false);

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
            // It's weird to repeat the condition here, but it allows the compiler to track nullability of other properties
            if (r.IsTimedOut)
            {
                _ = AppendCommandsWhenReadyAsync(r.Wrapper, r.PendingLoadTask, r.Stopwatch, ct);
            }
        }

        return new RegisterAndLoadSummary(totalCommands, totalDockBands);
    }

    private async Task<ExtensionStartResult> TryStartExtensionAsync(IExtensionWrapper extension)
    {
        Logger.LogDebug($"Starting {extension.PackageFullName}");
        var sw = Stopwatch.StartNew();
        var ct = _currentExtensionLoadCancellationToken;
        var startTask = extension.StartExtensionAsync();
        try
        {
            await startTask.WaitAsync(ExtensionStartTimeout, ct).ConfigureAwait(false);
            Logger.LogInfo($"Started extension {extension.PackageFullName} in {sw.ElapsedMilliseconds} ms");
            return ExtensionStartResult.Started(extension, new CommandProviderWrapper(extension, _taskScheduler, _commandProviderCache));
        }
        catch (TimeoutException)
        {
            Logger.LogWarning($"Starting extension {extension.PackageFullName} timed out after {sw.ElapsedMilliseconds} ms, continuing in background");
            return ExtensionStartResult.TimedOut(extension, startTask, sw);
        }
        catch (OperationCanceledException)
        {
            Logger.LogDebug($"Starting extension {extension.PackageFullName} was cancelled after {sw.ElapsedMilliseconds} ms");
            return ExtensionStartResult.Failed(extension);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to start extension {extension.PackageFullName} after {sw.ElapsedMilliseconds} ms: {ex}");
            return ExtensionStartResult.Failed(extension);
        }
    }

    private async Task StartExtensionWhenReadyAsync(
        IExtensionWrapper extension,
        Task startTask,
        Stopwatch sw,
        CancellationToken ct)
    {
        try
        {
            await startTask.WaitAsync(BackgroundStartTimeout, ct).ConfigureAwait(false);

            var wrapper = new CommandProviderWrapper(extension, _taskScheduler, _commandProviderCache);
            Logger.LogInfo($"Late-started extension {extension.PackageFullName} in {sw.ElapsedMilliseconds} ms, loading commands and bands");

            await RegisterAndLoadCommandsAsync([wrapper], ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Reload happened -- discard stale results
        }
        catch (Exception ex)
        {
            Logger.LogError($"Background start/load of extension {extension.PackageFullName} failed after {sw.ElapsedMilliseconds} ms: {ex}");
        }
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
            Logger.LogInfo($"Loaded {commandCount} command(s) and {dockBandCount} band(s) from {wrapper.ExtensionHost?.Extension?.PackageFullName} in {sw.ElapsedMilliseconds} ms");
            return CommandLoadResult.Loaded(wrapper, result);
        }
        catch (TimeoutException)
        {
            Logger.LogWarning($"Loading commands and bands from {wrapper.ExtensionHost?.Extension?.PackageFullName} timed out after {sw.ElapsedMilliseconds} ms, continuing in background");
            return CommandLoadResult.TimedOut(wrapper, loadTask, sw);
        }
        catch (OperationCanceledException)
        {
            Logger.LogDebug($"Loading commands and bands from {wrapper.ExtensionHost?.Extension?.PackageFullName} was cancelled after {sw.ElapsedMilliseconds} ms");
            return CommandLoadResult.Failed(wrapper);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to load commands and bands for extension {wrapper.ExtensionHost?.Extension?.PackageFullName} after {sw.ElapsedMilliseconds} ms: {ex}");
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

            Logger.LogInfo($"Late-loaded {commands?.Count ?? 0} command(s) and {dockBands?.Count ?? 0} band(s) from {wrapper.ExtensionHost?.Extension?.PackageFullName} in {sw.ElapsedMilliseconds} ms");
        }
        catch (OperationCanceledException)
        {
            // Reload happened - discard stale results
        }
        catch (Exception ex)
        {
            Logger.LogError($"Background loading of commands and bands from {wrapper.ExtensionHost?.Extension?.PackageFullName} failed after {sw.ElapsedMilliseconds} ms: {ex}");
        }
    }

    private void ExtensionService_OnExtensionRemoved(IExtensionService sender, IEnumerable<IExtensionWrapper> extensions)
    {
        // When we get an extension uninstall event, hop off to a BG thread
        _ = Task.Run(
            async () =>
            {
                // Then find all the top-level commands that belonged to that extension
                List<TopLevelViewModel> commandsToRemove = [];
                List<TopLevelViewModel> bandsToRemove = [];
                foreach (var extension in extensions)
                {
                    lock (TopLevelCommands)
                    {
                        foreach (var command in TopLevelCommands)
                        {
                            var host = command.ExtensionHost;
                            if (host?.Extension == extension)
                            {
                                commandsToRemove.Add(command);
                            }
                        }
                    }

                    lock (_dockBandsLock)
                    {
                        foreach (var band in DockBands)
                        {
                            var host = band.ExtensionHost;
                            if (host?.Extension == extension)
                            {
                                bandsToRemove.Add(band);
                            }
                        }
                    }
                }

                // Then back on the UI thread (remember, TopLevelCommands is
                // Observable, so you can't touch it on the BG thread)...
                await Task.Factory.StartNew(
                () =>
                {
                    // ... remove all the deleted commands.
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

    public void Receive(PinCommandItemMessage message)
    {
        var wrapper = LookupProvider(message.ProviderId);
        wrapper?.PinCommand(message.CommandId, _serviceProvider);
    }

    public void Receive(UnpinCommandItemMessage message)
    {
        var wrapper = LookupProvider(message.ProviderId);
        wrapper?.UnpinCommand(message.CommandId, _serviceProvider);
    }

    public void Receive(PinToDockMessage message)
    {
        if (LookupProvider(message.ProviderId) is CommandProviderWrapper wrapper)
        {
            if (message.Pin)
            {
                wrapper?.PinDockBand(message.CommandId, _serviceProvider);
            }
            else
            {
                wrapper?.UnpinDockBand(message.CommandId, _serviceProvider);
            }
        }
    }

    public CommandProviderWrapper? LookupProvider(string providerId)
    {
        lock (_commandProvidersLock)
        {
            return _builtInCommands.FirstOrDefault(w => w.ProviderId == providerId)
                   ?? _extensionCommandProviders.FirstOrDefault(w => w.ProviderId == providerId);
        }
    }

    internal bool IsProviderActive(string id)
    {
        lock (_commandProvidersLock)
        {
            return _builtInCommands.Any(wrapper => wrapper.Id == id && wrapper.IsActive)
                   || _extensionCommandProviders.Any(wrapper => wrapper.Id == id && wrapper.IsActive);
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
        _extensionLoadCts.Cancel();
        _extensionLoadCts.Dispose();
        _reloadCommandsGate.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed class ExtensionStartResult
    {
        public IExtensionWrapper Extension { get; }

        public CommandProviderWrapper? Wrapper { get; private init; }

        public Task? PendingStartTask { get; private init; }

        public Stopwatch? Stopwatch { get; private init; }

        [MemberNotNullWhen(true, nameof(Wrapper))]
        public bool IsStarted => Wrapper is not null;

        [MemberNotNullWhen(true, nameof(PendingStartTask), nameof(Stopwatch))]
        public bool IsTimedOut => PendingStartTask is not null;

        private ExtensionStartResult(IExtensionWrapper extension)
        {
            Extension = extension;
        }

        public static ExtensionStartResult Started(IExtensionWrapper extension, CommandProviderWrapper wrapper)
        {
            return new ExtensionStartResult(extension) { Wrapper = wrapper };
        }

        public static ExtensionStartResult TimedOut(IExtensionWrapper extension, Task pendingStartTask, Stopwatch sw)
        {
            return new ExtensionStartResult(extension) { PendingStartTask = pendingStartTask, Stopwatch = sw };
        }

        public static ExtensionStartResult Failed(IExtensionWrapper extension)
        {
            return new ExtensionStartResult(extension);
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
