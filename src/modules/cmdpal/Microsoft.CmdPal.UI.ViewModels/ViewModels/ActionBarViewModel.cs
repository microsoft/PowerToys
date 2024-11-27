// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ActionBarViewModel : ObservableObject
{
    public ListItemViewModel? SelectedItem
    {
        get => field;
        set
        {
            field = value;
            SetSelectedItem(value);
        }
    }

    [ObservableProperty]
    public partial string ActionName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool MoreCommandsAvailable { get; set; } = false;

    [ObservableProperty]
    public partial ObservableCollection<ActionBarContextItemViewModel> ContextActions { get; set; } = [];

    public ActionBarViewModel()
    {
    }

    private void SetSelectedItem(ListItemViewModel? value)
    {
        if (value != null)
        {
            ActionName = value.Command.Name;

            if (value.HasMoreCommands)
            {
                MoreCommandsAvailable = true;
                ContextActions = new(value.AllCommands
                    .Select(command => new ActionBarContextItemViewModel(command)));
            }
            else
            {
                MoreCommandsAvailable = false;
            }
        }
        else
        {
            ActionName = string.Empty;
        }
    }

    // InvokeItemCommand is what this will be in Xaml due to source generator
    [RelayCommand]
    private void InvokeItem(ActionBarContextItemViewModel item)
    {
        WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(item.Command));
    }
}
