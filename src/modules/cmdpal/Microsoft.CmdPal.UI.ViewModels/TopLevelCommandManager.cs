// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class TopLevelCommandManager(IServiceProvider _serviceProvider) : ObservableObject
{
    private readonly List<CommandProviderWrapper> _builtInCommands = [];
    private readonly List<CommandProviderWrapper> _extensionCommandProviders = [];

    public ObservableCollection<TopLevelCommandWrapper> TopLevelCommands { get; set; } = [];

    [ObservableProperty]
    public partial bool IsLoading { get; private set; } = true;

    public IEnumerable<CommandProviderWrapper> CommandProviders => _builtInCommands.Concat(_extensionCommandProviders);

    public async Task<bool> LoadBuiltinsAsync()
    {
        _builtInCommands.Clear();

        // Load built-In commands first. These are all in-proc, and
        // owned by our ServiceProvider.
        var builtInCommands = _serviceProvider.GetServices<ICommandProvider>();
        foreach (var provider in builtInCommands)
        {
            CommandProviderWrapper wrapper = new(provider);
            _builtInCommands.Add(wrapper);
            await LoadTopLevelCommandsFromProvider(wrapper);
        }

        return true;
    }

    private async Task LoadTopLevelCommandsFromProvider(CommandProviderWrapper commandProvider)
    {
        await commandProvider.LoadTopLevelCommands();
        foreach (var i in commandProvider.TopLevelItems)
        {
            TopLevelCommands.Add(new(new(i), false));
        }

        foreach (var i in commandProvider.FallbackItems)
        {
            TopLevelCommands.Add(new(new(i), true));
        }

        commandProvider.CommandsChanged += CommandProvider_CommandsChanged;
    }

    private void CommandProvider_CommandsChanged(CommandProviderWrapper sender, ItemsChangedEventArgs args)
    {
        // By all accounts, we're already on a background thread (the COM call
        // to handle the event shouldn't be on the main thread.). But just to
        // be sure we don't block the caller, hop off this thread
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
    private async Task UpdateCommandsForProvider(CommandProviderWrapper sender, ItemsChangedEventArgs args)
    {
        // Work on a clone of the list, so that we can just do one atomic
        // update to the actual observable list at the end
        List<TopLevelCommandWrapper> clone = [.. TopLevelCommands];
        List<TopLevelCommandWrapper> newItems = [];
        var startIndex = -1;
        var firstCommand = sender.TopLevelItems[0];
        var commandsToRemove = sender.TopLevelItems.Length + sender.FallbackItems.Length;

        // Tricky: all Commands from a single provider get added to the
        // top-level list all together, in a row. So if we find just the first
        // one, we can slice it out and insert the new ones there.
        for (var i = 0; i < clone.Count; i++)
        {
            var wrapper = clone[i];
            try
            {
                var thisCommand = wrapper.Model.Unsafe;
                if (thisCommand != null)
                {
                    var isTheSame = thisCommand == firstCommand;
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

        // Fetch the new items
        await sender.LoadTopLevelCommands();
        foreach (var i in sender.TopLevelItems)
        {
            newItems.Add(new(new(i), false));
        }

        foreach (var i in sender.FallbackItems)
        {
            newItems.Add(new(new(i), true));
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

    // Load commands from our extensions.
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
        var extensions = await extensionService.GetInstalledExtensionsAsync();
        _extensionCommandProviders.Clear();
        foreach (var extension in extensions)
        {
            try
            {
                await extension.StartExtensionAsync();
                CommandProviderWrapper wrapper = new(extension);
                _extensionCommandProviders.Add(wrapper);
                await LoadTopLevelCommandsFromProvider(wrapper);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        IsLoading = false;

        return true;
    }

    public TopLevelCommandWrapper? LookupCommand(string id)
    {
        foreach (var command in TopLevelCommands)
        {
            if (command.Id == id)
            {
                return command;
            }
        }

        return null;
    }
}
