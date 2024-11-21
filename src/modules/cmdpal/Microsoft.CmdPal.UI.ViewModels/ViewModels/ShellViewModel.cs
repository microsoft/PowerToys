// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using Microsoft.CmdPal.Models;
using Microsoft.CmdPal.UI.Pages;
using Microsoft.CmdPal.UI.ViewModels.Messages;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isLoaded = false;

    public ObservableCollection<CommandProviderWrapper> ActionsProvider { get; set; } = [];

    public ObservableCollection<ExtensionObject<IListItem>> TopLevelCommands { get; set; } = [];

    private readonly IEnumerable<ICommandProvider> _builtInCommands;

    public ShellViewModel(IEnumerable<ICommandProvider> builtInCommands)
    {
        _builtInCommands = builtInCommands;
    }

    [RelayCommand]
    public async Task<bool> LoadAsync()
    {
        // Load Built In Commands First
        foreach (var provider in _builtInCommands)
        {
            CommandProviderWrapper wrapper = new(provider);
            ActionsProvider.Add(wrapper);

            await LoadTopLevelCommandsFromProvider(wrapper);
        }

        IsLoaded = true;

        // TODO: would want to hydrate this from our services provider in the View layer, need to think about construction here...
        WeakReferenceMessenger.Default.Send<NavigateToListMessage>(new(new(new MainListPage(this))));
        return true;
    }

    private async Task LoadTopLevelCommandsFromProvider(CommandProviderWrapper commandProvider)
    {
        await commandProvider.LoadTopLevelCommands();
        foreach (var i in commandProvider.TopLevelItems)
        {
            TopLevelCommands.Add(new(new ListItem(i)));
        }
    }
}
