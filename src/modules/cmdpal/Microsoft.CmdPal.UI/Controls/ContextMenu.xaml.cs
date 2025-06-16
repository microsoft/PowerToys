// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Ext.System;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.Views;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class ContextMenu : UserControl,
    IRecipient<OpenContextMenuMessage>,
    IRecipient<TryCommandKeybindingMessage>
{
    public ContextMenuStackViewModel? ViewModel { get; set; }

    public ContextMenu()
    {
        this.InitializeComponent();

        // RegisterAll isn't AOT compatible
        WeakReferenceMessenger.Default.Register<OpenContextMenuMessage>(this);
        WeakReferenceMessenger.Default.Register<TryCommandKeybindingMessage>(this);

        if (ViewModel != null)
        {
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    public void Receive(OpenContextMenuMessage message)
    {
        UpdateUiForStackChange();
    }

    public void Receive(TryCommandKeybindingMessage msg)
    {
        var result = ViewModel?.CheckKeybinding(msg.Ctrl, msg.Alt, msg.Shift, msg.Win, msg.Key);

        if (result != null)
        {
            WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(result.Command.Model, result.Model));
            WeakReferenceMessenger.Default.Send<CloseContextMenuMessage>();
            msg.Handled = true;
        }
    }

    private void CommandsDropdown_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is CommandContextItemViewModel item)
        {
            WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(item.Command.Model, item.Model));
            WeakReferenceMessenger.Default.Send<CloseContextMenuMessage>();
            UpdateUiForStackChange();
        }
    }

    private void CommandsDropdown_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        var ctrlPressed = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
        var altPressed = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu).HasFlag(CoreVirtualKeyStates.Down);
        var shiftPressed = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);
        var winPressed = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.LeftWindows).HasFlag(CoreVirtualKeyStates.Down) ||
            InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.RightWindows).HasFlag(CoreVirtualKeyStates.Down);

        var result = ViewModel?.CheckKeybinding(ctrlPressed, altPressed, shiftPressed, winPressed, e.Key);

        if (result != null)
        {
            WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(result.Command.Model, result.Model));
            WeakReferenceMessenger.Default.Send<CloseContextMenuMessage>();
            UpdateUiForStackChange();
            e.Handled = true;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        var prop = e.PropertyName;
        if (prop == nameof(ContextMenuStackViewModel.FilteredItems))
        {
            UpdateUiForStackChange();
        }
    }

    private void ContextFilterBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel?.SetSearchText(ContextFilterBox.Text);

        if (CommandsDropdown.SelectedIndex == -1)
        {
            CommandsDropdown.SelectedIndex = 0;
        }
    }

    private void ContextFilterBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var ctrlPressed = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
        var altPressed = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu).HasFlag(CoreVirtualKeyStates.Down);
        var shiftPressed = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);
        var winPressed = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.LeftWindows).HasFlag(CoreVirtualKeyStates.Down) ||
            InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.RightWindows).HasFlag(CoreVirtualKeyStates.Down);

        if (e.Key == VirtualKey.Enter)
        {
            if (CommandsDropdown.SelectedItem is CommandContextItemViewModel item)
            {
                WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(item.Command.Model, item.Model));
                WeakReferenceMessenger.Default.Send<CloseContextMenuMessage>();
                UpdateUiForStackChange();
                e.Handled = true;
            }
        }
        else if (e.Key == VirtualKey.Escape ||
            (e.Key == VirtualKey.Left && altPressed))
        {
            WeakReferenceMessenger.Default.Send<CloseContextMenuMessage>();
            e.Handled = true;
        }

        CommandsDropdown_KeyDown(sender, e);
    }

    private void ContextFilterBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Up)
        {
            // navigate previous
            if (CommandsDropdown.SelectedIndex > 0)
            {
                CommandsDropdown.SelectedIndex--;
            }
            else
            {
                CommandsDropdown.SelectedIndex = CommandsDropdown.Items.Count - 1;
            }

            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Down)
        {
            // navigate next
            if (CommandsDropdown.SelectedIndex < CommandsDropdown.Items.Count - 1)
            {
                CommandsDropdown.SelectedIndex++;
            }
            else
            {
                CommandsDropdown.SelectedIndex = 0;
            }

            e.Handled = true;
        }
    }

    private void UpdateUiForStackChange()
    {
        ContextFilterBox.Text = string.Empty;
        ViewModel?.SetSearchText(string.Empty);
        CommandsDropdown.SelectedIndex = 0;
        ContextFilterBox.Focus(FocusState.Programmatic);
    }
}
