// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
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

    // [NotifyPropertyChangedFor(nameof(ContextMenu))]
    public partial ObservableCollection<ContextMenuStackViewModel> ContextMenuStack { get; set; } = [];

    public ContextMenuStackViewModel? ContextMenu => ContextMenuStack.LastOrDefault();

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
            ContextMenuStack.Clear();
            ContextMenuStack.Add(new ContextMenuStackViewModel([.. SelectedItem.AllCommands]));

            // ContextCommands = [.. SelectedItem.AllCommands];
            OnPropertyChanged(nameof(ContextMenu));
        }
        else
        {
            ShouldShowContextMenu = false;
        }
    }

    // InvokeItemCommand is what this will be in Xaml due to source generator
    // this comes in when an item in the list is tapped
    // [RelayCommand]
    public bool InvokeItem(CommandContextItemViewModel item) =>
        PerformCommand(item);

    // WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(item.Command.Model, item.Model));

    // this comes in when the primary button is tapped
    public void InvokePrimaryCommand()
    {
        PerformCommand(SecondaryCommand);

        // if (PrimaryCommand != null)
        // {
        //    WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(PrimaryCommand.Command.Model, PrimaryCommand.Model));
        // }
    }

    // this comes in when the secondary button is tapped
    public void InvokeSecondaryCommand()
    {
        PerformCommand(SecondaryCommand);

        // if (SecondaryCommand != null)
        // {
        //    WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(SecondaryCommand.Command.Model, SecondaryCommand.Model));
        // }
    }

    public bool CheckKeybinding(bool ctrl, bool alt, bool shift, bool win, VirtualKey key)
    {
        if (_contextKeybindings != null)
        {
            // Does the pressed key match any of the keybindings?
            var pressedKeyChord = KeyChordHelpers.FromModifiers(ctrl, alt, shift, win, key, 0);
            if (_contextKeybindings.TryGetValue(pressedKeyChord, out var item))
            {
                PerformCommand(item);

                // WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(item));
                return true;
            }
        }

        return false;
    }

    private bool PerformCommand(CommandItemViewModel? command)
    {
        if (command == null)
        {
            return false;
        }

        if (command.HasMoreCommands)
        {
            var newContext = command.AllCommands;
            ContextMenuStack.Add(new ContextMenuStackViewModel(newContext));
            OnPropertyChanging(nameof(ContextMenu));
            OnPropertyChanged(nameof(ContextMenu));
            return false;
        }
        else
        {
            WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(command.Command.Model, command.Model));
            return true;
        }
    }

    public void PopContextStack()
    {
        if (ContextMenuStack.Count > 1)
        {
            ContextMenuStack.RemoveAt(ContextMenuStack.Count - 1);
        }

        OnPropertyChanging(nameof(ContextMenu));
        OnPropertyChanged(nameof(ContextMenu));
    }

    public void ClearContextStack()
    {
        while (ContextMenuStack.Count > 1)
        {
            ContextMenuStack.RemoveAt(ContextMenuStack.Count - 1);
        }

        OnPropertyChanging(nameof(ContextMenu));
        OnPropertyChanged(nameof(ContextMenu));
    }
}
