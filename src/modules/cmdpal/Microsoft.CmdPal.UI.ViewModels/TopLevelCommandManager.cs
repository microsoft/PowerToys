// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.Core.Common.Helpers;
using Microsoft.CmdPal.Core.Common.Services;
using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class TopLevelCommandManager : ObservableObject,
    IRecipient<ReloadCommandsMessage>,
    IPageContext,
    IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TaskScheduler _taskScheduler;

    private readonly List<CommandProviderWrapper> _builtInCommands = [];
    private readonly List<CommandProviderWrapper> _extensionCommandProviders = [];
    private readonly Lock _commandProvidersLock = new();
    private readonly SupersedingAsyncGate _reloadCommandsGate;

    TaskScheduler IPageContext.Scheduler => _taskScheduler;

    public TopLevelCommandManager(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _taskScheduler = _serviceProvider.GetService<TaskScheduler>()!;
        WeakReferenceMessenger.Default.Register<ReloadCommandsMessage>(this);
        _reloadCommandsGate = new(ReloadAllCommandsAsyncCore);
    }

    public ObservableCollection<TopLevelViewModel> TopLevelCommands { get; set; } = [];

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

            var commands = await LoadTopLevelCommandsFromProvider(wrapper);
            lock (TopLevelCommands)
            {
                foreach (var c in commands)
                {
                    TopLevelCommands.Add(c);
                }
            }
        }

        s.Stop();

        Logger.LogDebug($"Loading built-ins took {s.ElapsedMilliseconds}ms");

        return true;
    }

    // May be called from a background thread
    private async Task<IEnumerable<TopLevelViewModel>> LoadTopLevelCommandsFromProvider(CommandProviderWrapper commandProvider)
    {
        WeakReference<IPageContext> weakSelf = new(this);

        await commandProvider.LoadTopLevelCommands(_serviceProvider, weakSelf);

        var commands = await Task.Factory.StartNew(
            () =>
            {
                List<TopLevelViewModel> commands = [];
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

                return commands;
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
        WeakReference<IPageContext> weakSelf = new(this);
        await sender.LoadTopLevelCommands(_serviceProvider, weakSelf);

        List<TopLevelViewModel> newItems = [.. sender.TopLevelItems];
        foreach (var i in sender.FallbackItems)
        {
            if (i.IsEnabled)
            {
                newItems.Add(i);
            }
        }

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
        var extensionService = _serviceProvider.GetService<IExtensionService>()!;
        await extensionService.SignalStopExtensionsAsync();

        lock (TopLevelCommands)
        {
            TopLevelCommands.Clear();
        }

        await LoadBuiltinsAsync();
        _ = Task.Run(LoadExtensionsAsync);
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

        var extensions = (await extensionService.GetInstalledExtensionsAsync()).ToImmutableList();
        lock (_commandProvidersLock)
        {
            _extensionCommandProviders.Clear();
        }

        if (extensions is not null)
        {
            await StartExtensionsAndGetCommands(extensions);
        }

        extensionService.OnExtensionAdded += ExtensionService_OnExtensionAdded;
        extensionService.OnExtensionRemoved += ExtensionService_OnExtensionRemoved;

        IsLoading = false;

        // Send on the current thread; receivers should marshal to UI if needed
        WeakReferenceMessenger.Default.Send<ReloadFinishedMessage>();

        return true;
    }

    private void ExtensionService_OnExtensionAdded(IExtensionService sender, IEnumerable<IExtensionWrapper> extensions)
    {
        // When we get an extension install event, hop off to a BG thread
        _ = Task.Run(async () =>
        {
            // for each newly installed extension, start it and get commands
            // from it. One single package might have more than one
            // IExtensionWrapper in it.
            await StartExtensionsAndGetCommands(extensions);
        });
    }

    private async Task StartExtensionsAndGetCommands(IEnumerable<IExtensionWrapper> extensions)
    {
        var timer = new Stopwatch();
        timer.Start();

        // Start all extensions in parallel
        var startTasks = extensions.Select(StartExtensionWithTimeoutAsync);

        // Wait for all extensions to start
        var wrappers = (await Task.WhenAll(startTasks)).Where(wrapper => wrapper is not null).Select(w => w!).ToList();

        lock (_commandProvidersLock)
        {
            _extensionCommandProviders.AddRange(wrappers);
        }

        // Load the commands from the providers in parallel
        var loadTasks = wrappers.Select(LoadCommandsWithTimeoutAsync);

        var commandSets = (await Task.WhenAll(loadTasks)).Where(results => results is not null).Select(r => r!).ToList();

        lock (TopLevelCommands)
        {
            foreach (var commands in commandSets)
            {
                foreach (var c in commands)
                {
                    TopLevelCommands.Add(c);
                }
            }
        }

        timer.Stop();
        Logger.LogDebug($"Loading extensions took {timer.ElapsedMilliseconds} ms");
    }

    private async Task<CommandProviderWrapper?> StartExtensionWithTimeoutAsync(IExtensionWrapper extension)
    {
        Logger.LogDebug($"Starting {extension.PackageFullName}");
        try
        {
            await extension.StartExtensionAsync().WaitAsync(TimeSpan.FromSeconds(10));
            return new CommandProviderWrapper(extension, _taskScheduler);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to start extension {extension.PackageFullName}: {ex}");
            return null; // Return null for failed extensions
        }
    }

    private async Task<IEnumerable<TopLevelViewModel>?> LoadCommandsWithTimeoutAsync(CommandProviderWrapper wrapper)
    {
        try
        {
            return await LoadTopLevelCommandsFromProvider(wrapper!).WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (TimeoutException)
        {
            Logger.LogError($"Loading commands from {wrapper!.ExtensionHost?.Extension?.PackageFullName} timed out");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to load commands for extension {wrapper!.ExtensionHost?.Extension?.PackageFullName}: {ex}");
        }

        return null;
    }

    private void ExtensionService_OnExtensionRemoved(IExtensionService sender, IEnumerable<IExtensionWrapper> extensions)
    {
        // When we get an extension uninstall event, hop off to a BG thread
        _ = Task.Run(
            async () =>
            {
                // Then find all the top-level commands that belonged to that extension
                List<TopLevelViewModel> commandsToRemove = [];
                lock (TopLevelCommands)
                {
                    foreach (var extension in extensions)
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

    public void Receive(ReloadCommandsMessage message) =>
        ReloadAllCommandsAsync().ConfigureAwait(false);

    void IPageContext.ShowException(Exception ex, string? extensionHint)
    {
        var message = DiagnosticsHelper.BuildExceptionMessage(ex, extensionHint ?? "TopLevelCommandManager");
        CommandPaletteHost.Instance.Log(message);
    }

    internal bool IsProviderActive(string id)
    {
        lock (_commandProvidersLock)
        {
            return _builtInCommands.Any(wrapper => wrapper.Id == id && wrapper.IsActive)
                   || _extensionCommandProviders.Any(wrapper => wrapper.Id == id && wrapper.IsActive);
        }
    }

    public void Dispose()
    {
        _reloadCommandsGate.Dispose();
        GC.SuppressFinalize(this);
    }
}
