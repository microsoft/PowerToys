// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ActionBarViewModel : ObservableObject,
    IRecipient<UpdateActionBarMessage>
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
    [NotifyPropertyChangedFor(nameof(HasPrimaryCommand))]
    public partial CommandItemViewModel? PrimaryAction { get; set; }

    public bool HasPrimaryCommand => PrimaryAction != null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSecondaryCommand))]
    public partial CommandItemViewModel? SecondaryAction { get; set; }

    public bool HasSecondaryCommand => SecondaryAction != null;

    [ObservableProperty]
    public partial bool ShouldShowContextMenu { get; set; } = false;

    [ObservableProperty]
    public partial PageViewModel? CurrentPage { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<CommandContextItemViewModel> ContextActions { get; set; } = [];

    public ActionBarViewModel()
    {
        WeakReferenceMessenger.Default.Register<UpdateActionBarMessage>(this);
    }

    public void Receive(UpdateActionBarMessage message) => SelectedItem = message.ViewModel;

    private void SetSelectedItem(ListItemViewModel? value)
    {
        if (value != null)
        {
            PrimaryAction = value;
            SecondaryAction = value.SecondaryCommand;

            if (value.MoreCommands.Count > 1)
            {
                ShouldShowContextMenu = true;
                ContextActions = [.. value.AllCommands];
            }
            else
            {
                ShouldShowContextMenu = false;
            }
        }
        else
        {
            PrimaryAction = null;
            SecondaryAction = null;
            ShouldShowContextMenu = false;
        }
    }

    // InvokeItemCommand is what this will be in Xaml due to source generator
    [RelayCommand]
    private void InvokeItem(CommandContextItemViewModel item) => WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(item.Command));
}
