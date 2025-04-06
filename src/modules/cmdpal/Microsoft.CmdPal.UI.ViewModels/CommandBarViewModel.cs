// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class CommandBarViewModel : ObservableObject,
    IRecipient<UpdateCommandBarMessage>
{
    public ICommandBarContext? SelectedItem
    {
        get => field;
        set
        {
            if (field != null)
            {
                field.PropertyChanged -= SelectedItemPropertyChanged;
            }

            field = value;
            SetSelectedItem(value);
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPrimaryCommand))]
    public partial CommandItemViewModel? PrimaryCommand { get; set; }

    public bool HasPrimaryCommand => PrimaryCommand != null && PrimaryCommand.ShouldBeVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSecondaryCommand))]
    public partial CommandItemViewModel? SecondaryCommand { get; set; }

    public bool HasSecondaryCommand => SecondaryCommand != null;

    [ObservableProperty]
    public partial bool ShouldShowContextMenu { get; set; } = false;

    [ObservableProperty]
    public partial PageViewModel? CurrentPage { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<CommandContextItemViewModel> ContextCommands { get; set; } = [];

    public CommandBarViewModel()
    {
        WeakReferenceMessenger.Default.Register<UpdateCommandBarMessage>(this);
    }

    public void Receive(UpdateCommandBarMessage message) => SelectedItem = message.ViewModel;

    private void SetSelectedItem(ICommandBarContext? value)
    {
        if (value != null)
        {
            PrimaryCommand = value.PrimaryCommand;
            value.PropertyChanged += SelectedItemPropertyChanged;
        }
        else
        {
            if (SelectedItem != null)
            {
                SelectedItem.PropertyChanged -= SelectedItemPropertyChanged;
            }

            PrimaryCommand = null;
        }

        UpdateContextItems();
    }

    private void SelectedItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SelectedItem.HasMoreCommands):
                UpdateContextItems();
                break;
        }
    }

    private void UpdateContextItems()
    {
        if (SelectedItem == null)
        {
            SecondaryCommand = null;
            ShouldShowContextMenu = false;
            return;
        }

        SecondaryCommand = SelectedItem.SecondaryCommand;

        if (SelectedItem.MoreCommands.Count() > 1)
        {
            ShouldShowContextMenu = true;
            ContextCommands = [.. SelectedItem.AllCommands];
        }
        else
        {
            ShouldShowContextMenu = false;
        }
    }

    // InvokeItemCommand is what this will be in Xaml due to source generator
    // this comes in when an item in the list is tapped
    [RelayCommand]
    private void InvokeItem(CommandContextItemViewModel item) =>
       WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(item.Command.Model, item.Model));

    // this comes in when the primary button is tapped
    public void InvokePrimaryCommand()
    {
        if (PrimaryCommand != null)
        {
            WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(PrimaryCommand.Command.Model, PrimaryCommand.Model));
        }
    }

    // this comes in when the secondary button is tapped
    public void InvokeSecondaryCommand()
    {
        if (SecondaryCommand != null)
        {
            WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(SecondaryCommand.Command.Model, SecondaryCommand.Model));
        }
    }
}
