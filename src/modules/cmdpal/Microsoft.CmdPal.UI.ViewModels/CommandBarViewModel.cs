// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class CommandBarViewModel : ObservableObject,
    IRecipient<UpdateCommandBarMessage>,
    IRecipient<UpdateItemKeybindingsMessage>
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

            OnPropertyChanged(nameof(SelectedItem));
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

    private Dictionary<KeyChord, CommandContextItemViewModel>? _contextKeybindings;

    public CommandBarViewModel()
    {
        WeakReferenceMessenger.Default.Register<UpdateCommandBarMessage>(this);
        WeakReferenceMessenger.Default.Register<UpdateItemKeybindingsMessage>(this);
    }

    public void Receive(UpdateCommandBarMessage message) => SelectedItem = message.ViewModel;

    public void Receive(UpdateItemKeybindingsMessage message) => _contextKeybindings = message.Keys;

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
            ContextCommands = [.. SelectedItem.AllCommands.Where(c => c.ShouldBeVisible)];
        }
        else
        {
            ShouldShowContextMenu = false;
        }

        OnPropertyChanged(nameof(HasSecondaryCommand));
        OnPropertyChanged(nameof(SecondaryCommand));
        OnPropertyChanged(nameof(ShouldShowContextMenu));
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

    public bool CheckKeybinding(bool ctrl, bool alt, bool shift, bool win, VirtualKey key)
    {
        if (_contextKeybindings != null)
        {
            // Does the pressed key match any of the keybindings?
            var pressedKeyChord = KeyChordHelpers.FromModifiers(ctrl, alt, shift, win, key, 0);
            if (_contextKeybindings.TryGetValue(pressedKeyChord, out var item))
            {
                // TODO GH #245: This is a bit of a hack, but we need to make sure that the keybindings are updated before we send the message
                // so that the correct item is activated.
                WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(item));
                return true;
            }
        }

        return false;
    }
}
