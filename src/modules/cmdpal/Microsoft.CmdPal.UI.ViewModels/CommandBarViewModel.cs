// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;
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

    // private Dictionary<KeyChord, CommandContextItemViewModel>? _contextKeybindings;
    public CommandBarViewModel()
    {
        WeakReferenceMessenger.Default.Register<UpdateCommandBarMessage>(this);
        WeakReferenceMessenger.Default.Register<UpdateItemKeybindingsMessage>(this);
    }

    public void Receive(UpdateCommandBarMessage message) => SelectedItem = message.ViewModel;

    public void Receive(UpdateItemKeybindingsMessage message)
    {
        // if (ContextMenuStack.FirstOrDefault() is ContextMenuStackViewModel first) {
        //    first.SetKeybin
        // }
    }

    // _contextKeybindings = message.Keys;
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
            ContextMenuStack.Add(new ContextMenuStackViewModel(SelectedItem));

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
    public ContextKeybindingResult InvokeItem(CommandContextItemViewModel item) =>
        PerformCommand(item);

    // this comes in when the primary button is tapped
    public void InvokePrimaryCommand()
    {
        PerformCommand(SecondaryCommand);
    }

    // this comes in when the secondary button is tapped
    public void InvokeSecondaryCommand()
    {
        PerformCommand(SecondaryCommand);
    }

    public ContextKeybindingResult CheckKeybinding(bool ctrl, bool alt, bool shift, bool win, VirtualKey key)
    {
        var matchedItem = ContextMenu?.CheckKeybinding(ctrl, alt, shift, win, key);
        return matchedItem != null ? PerformCommand(matchedItem) : ContextKeybindingResult.Unhandled;
    }

    private ContextKeybindingResult PerformCommand(CommandItemViewModel? command)
    {
        if (command == null)
        {
            return ContextKeybindingResult.Unhandled;
        }

        if (command.HasMoreCommands)
        {
            ContextMenuStack.Add(new ContextMenuStackViewModel(command));
            OnPropertyChanging(nameof(ContextMenu));
            OnPropertyChanged(nameof(ContextMenu));
            return ContextKeybindingResult.KeepOpen;
        }
        else
        {
            WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(command.Command.Model, command.Model));
            return ContextKeybindingResult.Hide;
        }
    }

    public bool CanPopContextStack()
    {
        return ContextMenuStack.Count > 1;
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

public enum ContextKeybindingResult
{
    Unhandled,
    Hide,
    KeepOpen,
}
