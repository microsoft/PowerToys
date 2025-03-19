// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class TopLevelCommandManager : ObservableObject,
    IRecipient<ReloadCommandsMessage>,
    IPageContext
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TaskScheduler _taskScheduler;

    private readonly List<CommandProviderWrapper> _builtInCommands = [];
    private readonly List<CommandProviderWrapper> _extensionCommandProviders = [];

    TaskScheduler IPageContext.Scheduler => _taskScheduler;

    public TopLevelCommandManager(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _taskScheduler = _serviceProvider.GetService<TaskScheduler>()!;
        WeakReferenceMessenger.Default.Register<ReloadCommandsMessage>(this);
    }

    public ObservableCollection<TopLevelViewModel> TopLevelCommands { get; set; } = [];

    [ObservableProperty]
    public partial bool IsLoading { get; private set; } = true;

    public IEnumerable<CommandProviderWrapper> CommandProviders => _builtInCommands.Concat(_extensionCommandProviders);

    public async Task<bool> LoadBuiltinsAsync()
    {
        _builtInCommands.Clear();

        // Load built-In commands first. These are all in-proc, and
        // owned by our ServiceProvider.
        IEnumerable<ICommandProvider> builtInCommands = _serviceProvider.GetServices<ICommandProvider>();
        foreach (ICommandProvider provider in builtInCommands)
        {
            CommandProviderWrapper wrapper = new(provider, _taskScheduler);
            _builtInCommands.Add(wrapper);
            await LoadTopLevelCommandsFromProvider(wrapper);
        }

        return true;
    }

    // May be called from a background thread
    private async Task LoadTopLevelCommandsFromProvider(CommandProviderWrapper commandProvider)
    {
        WeakReference<IPageContext> weakSelf = new(this);

        await commandProvider.LoadTopLevelCommands(_serviceProvider, weakSelf);

        SettingsModel settings = _serviceProvider.GetService<SettingsModel>()!;
        Action<ICommandItem?, bool> makeAndAdd = (ICommandItem? i, bool fallback) =>
        {
            CommandItemViewModel commandItemViewModel = new(new(i), weakSelf);
            TopLevelViewModel topLevelViewModel = new(commandItemViewModel, fallback, commandProvider.ExtensionHost, settings, _serviceProvider);

            // TopLevelCommandItemWrapper wrapper = new(
            //    new(i), fallback, commandProvider.ExtensionHost, commandProvider.ProviderId, _serviceProvider);
            lock (TopLevelCommands)
            {
                TopLevelCommands.Add(topLevelViewModel);
            }
        };

        await Task.Factory.StartNew(
            () =>
            {
                // foreach (ICommandItem i in commandProvider.TopLevelItems)
                // {
                //    makeAndAdd(i, false);
                // }

                // foreach (IFallbackCommandItem i in commandProvider.FallbackItems)
                // {
                //    makeAndAdd(i, true);
                // }
                lock (TopLevelCommands)
                {
                    foreach (TopLevelViewModel item in commandProvider.TopLevelItems)
                    {
                        TopLevelCommands.Add(item);
                    }

                    foreach (TopLevelViewModel item in commandProvider.FallbackItems)
                    {
                        TopLevelCommands.Add(item);
                    }
                }
            },
            CancellationToken.None,
            TaskCreationOptions.None,
            _taskScheduler);

        commandProvider.CommandsChanged += CommandProvider_CommandsChanged;
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
        // Work on a clone of the list, so that we can just do one atomic
        // update to the actual observable list at the end
        List<TopLevelViewModel> clone = [.. TopLevelCommands];
        List<TopLevelViewModel> newItems = [];
        int startIndex = -1;
        TopLevelViewModel firstCommand = sender.TopLevelItems[0];
        int commandsToRemove = sender.TopLevelItems.Length + sender.FallbackItems.Length;

        // Tricky: all Commands from a single provider get added to the
        // top-level list all together, in a row. So if we find just the first
        // one, we can slice it out and insert the new ones there.
        for (int i = 0; i < clone.Count; i++)
        {
            TopLevelViewModel wrapper = clone[i];
            try
            {
                // TODO! this can be safer, we're not directly exposing ICommandItem's out of CPW anymore
                ICommandItem? thisCommand = wrapper.ItemViewModel.Model.Unsafe;
                if (thisCommand != null)
                {
                    bool isTheSame = thisCommand == firstCommand;
                    if (isTheSame)
                    {
                        startIndex = i;
                        break;
                    }
                }
            }
            catch
            {
            }
        }

        WeakReference<IPageContext> weakSelf = new(this);

        // Fetch the new items
        await sender.LoadTopLevelCommands(_serviceProvider, weakSelf);

        SettingsModel settings = _serviceProvider.GetService<SettingsModel>()!;

        foreach (TopLevelViewModel i in sender.TopLevelItems)
        {
            // CommandItemViewModel commandItemViewModel = new(new(i), weakSelf);
            // TopLevelViewModel topLevelViewModel = new(commandItemViewModel, false, sender.ExtensionHost, settings, _serviceProvider);
            newItems.Add(i);
        }

        foreach (TopLevelViewModel i in sender.FallbackItems)
        {
            // CommandItemViewModel commandItemViewModel = new(new(i), weakSelf);
            // TopLevelViewModel topLevelViewModel = new(commandItemViewModel, true, sender.ExtensionHost, settings, _serviceProvider);
            newItems.Add(i);
        }

        // Slice out the old commands
        if (startIndex != -1)
        {
            clone.RemoveRange(startIndex, commandsToRemove);
        }
        else
        {
            // ... or, just stick them at the end (this is unexpected)
            startIndex = clone.Count;
        }

        // add the new commands into the list at the place we found the old ones
        clone.InsertRange(startIndex, newItems);

        // now update the actual observable list with the new contents
        ListHelpers.InPlaceUpdateList(TopLevelCommands, clone);
    }

    public async Task ReloadAllCommandsAsync()
    {
        IsLoading = true;
        IExtensionService extensionService = _serviceProvider.GetService<IExtensionService>()!;
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
        IExtensionService extensionService = _serviceProvider.GetService<IExtensionService>()!;

        extensionService.OnExtensionAdded -= ExtensionService_OnExtensionAdded;
        extensionService.OnExtensionRemoved -= ExtensionService_OnExtensionRemoved;

        IEnumerable<IExtensionWrapper> extensions = await extensionService.GetInstalledExtensionsAsync();
        _extensionCommandProviders.Clear();
        if (extensions != null)
        {
            await StartExtensionsAndGetCommands(extensions);
        }

        extensionService.OnExtensionAdded += ExtensionService_OnExtensionAdded;
        extensionService.OnExtensionRemoved += ExtensionService_OnExtensionRemoved;

        IsLoading = false;

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
        // TODO This most definitely needs a lock
        foreach (IExtensionWrapper extension in extensions)
        {
            try
            {
                // start it ...
                await extension.StartExtensionAsync();

                // ... and fetch the command provider from it.
                CommandProviderWrapper wrapper = new(extension, _taskScheduler);
                _extensionCommandProviders.Add(wrapper);
                await LoadTopLevelCommandsFromProvider(wrapper);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }
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
                lock (TopLevelCommands)
                {
                    foreach (IExtensionWrapper extension in extensions)
                    {
                        foreach (TopLevelViewModel command in TopLevelCommands)
                        {
                            CommandPaletteHost host = command.ExtensionHost;
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
                            foreach (TopLevelViewModel deleted in commandsToRemove)
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
            foreach (TopLevelViewModel command in TopLevelCommands)
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
        string errorMessage = $"A bug occurred in {$"the \"{extensionHint}\"" ?? "an unknown's"} extension's code:\n{ex.Message}\n{ex.Source}\n{ex.StackTrace}\n\n";
        CommandPaletteHost.Instance.Log(errorMessage);
    }
}
