// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace Microsoft.CmdPal.Core.ViewModels;

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

            OnPropertyChanged(nameof(SelectedItem));
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPrimaryCommand))]
    public partial CommandItemViewModel? PrimaryCommand { get; set; }

    public bool HasPrimaryCommand => PrimaryCommand is not null && PrimaryCommand.ShouldBeVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSecondaryCommand))]
    public partial CommandItemViewModel? SecondaryCommand { get; set; }

    public bool HasSecondaryCommand => SecondaryCommand is not null;

    [ObservableProperty]
    public partial bool ShouldShowContextMenu { get; set; } = false;

    [ObservableProperty]
    public partial PageViewModel? CurrentPage { get; set; }

    public CommandBarViewModel()
    {
        WeakReferenceMessenger.Default.Register<UpdateCommandBarMessage>(this);
    }

    public void Receive(UpdateCommandBarMessage message) => SelectedItem = message.ViewModel;

    private void SetSelectedItem(ICommandBarContext? value)
    {
        if (value is not null)
        {
            PrimaryCommand = value.PrimaryCommand;
            value.PropertyChanged += SelectedItemPropertyChanged;
        }
        else
        {
            if (SelectedItem is not null)
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
        if (SelectedItem is null)
        {
            SecondaryCommand = null;
            ShouldShowContextMenu = false;
            return;
        }

        SecondaryCommand = SelectedItem.SecondaryCommand;

        ShouldShowContextMenu = SelectedItem.MoreCommands
            .OfType<CommandContextItemViewModel>()
            .Count() > 1;

        OnPropertyChanged(nameof(HasSecondaryCommand));
        OnPropertyChanged(nameof(SecondaryCommand));
        OnPropertyChanged(nameof(ShouldShowContextMenu));
    }

    // InvokeItemCommand is what this will be in Xaml due to source generator
    // this comes in when an item in the list is tapped
    // [RelayCommand]
    public ContextKeybindingResult InvokeItem(CommandContextItemViewModel item) =>
        PerformCommand(item);

    // this comes in when the primary button is tapped
    public void InvokePrimaryCommand()
    {
        PerformCommand(PrimaryCommand);
    }

    // this comes in when the secondary button is tapped
    public void InvokeSecondaryCommand()
    {
        PerformCommand(SecondaryCommand);
    }

    public ContextKeybindingResult CheckKeybinding(bool ctrl, bool alt, bool shift, bool win, VirtualKey key)
    {
        var keybindings = SelectedItem?.Keybindings();
        if (keybindings is not null)
        {
            // Does the pressed key match any of the keybindings?
            var pressedKeyChord = KeyChordHelpers.FromModifiers(ctrl, alt, shift, win, key, 0);
            if (keybindings.TryGetValue(pressedKeyChord, out var matchedItem))
            {
                return matchedItem is not null ? PerformCommand(matchedItem) : ContextKeybindingResult.Unhandled;
            }
        }

        return ContextKeybindingResult.Unhandled;
    }

    private ContextKeybindingResult PerformCommand(CommandItemViewModel? command)
    {
        if (command is null)
        {
            return ContextKeybindingResult.Unhandled;
        }

        if (command.HasMoreCommands)
        {
            WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(command.Command.Model, command.Model));
            return ContextKeybindingResult.KeepOpen;
        }
        else
        {
            WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(command.Command.Model, command.Model));
            return ContextKeybindingResult.Hide;
        }
    }
}

public enum ContextKeybindingResult
{
    Unhandled,
    Hide,
    KeepOpen,
}
