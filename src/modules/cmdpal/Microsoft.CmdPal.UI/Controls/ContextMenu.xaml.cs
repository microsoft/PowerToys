// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
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
    IRecipient<TryCommandKeybindingMessage>,
    IRecipient<CloseContextMenuMessage>,
    IRecipient<OpenContextMenuMessage>
{
    public ContextMenuStackViewModel? ViewModel { get; set; }

    public ContextMenu()
    {
        this.InitializeComponent();

        // RegisterAll isn't AOT compatible
        WeakReferenceMessenger.Default.Register<TryCommandKeybindingMessage>(this);
        WeakReferenceMessenger.Default.Register<CloseContextMenuMessage>(this);
        WeakReferenceMessenger.Default.Register<OpenContextMenuMessage>(this);
    }

    public void Receive(OpenContextMenuMessage msg)
    {
        UpdateUiForStackChange();
    }

    public void Receive(CloseContextMenuMessage msg)
    {
        UpdateUiForStackChange();
    }

    public void Receive(TryCommandKeybindingMessage msg)
    {
        var keyboundCommandModel = ViewModel?.CheckKeybinding(msg.Ctrl, msg.Alt, msg.Shift, msg.Win, msg.Key);

        if (keyboundCommandModel == null)
        {
            return;
        }

        var result = ViewModel?.InvokeItem(keyboundCommandModel);

        if (result == null)
        {
            return;
        }

        if (result == ContextKeybindingResult.Hide)
        {
            msg.Handled = true;
        }
        else if (result == ContextKeybindingResult.KeepOpen)
        {
            UpdateUiForStackChange();

            msg.Handled = true;
        }
        else if (result == ContextKeybindingResult.Unhandled)
        {
            msg.Handled = false;
        }
    }

    private void CommandsDropdown_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is CommandContextItemViewModel item)
        {
            ViewModel?.InvokeItem(item);
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

        var keyboundCommandModel = ViewModel?.CheckKeybinding(ctrlPressed, altPressed, shiftPressed, winPressed, e.Key);

        if (keyboundCommandModel == null)
        {
            return;
        }

        var result = ViewModel?.InvokeItem(keyboundCommandModel);

        if (result == null)
        {
            return;
        }
        else if (result == ContextKeybindingResult.Hide)
        {
            e.Handled = true;
            WeakReferenceMessenger.Default.Send<CloseContextMenuMessage>();
            WeakReferenceMessenger.Default.Send<FocusSearchBoxMessage>();
        }
        else if (result == ContextKeybindingResult.KeepOpen)
        {
            e.Handled = true;
        }
        else if (result == ContextKeybindingResult.Unhandled)
        {
            e.Handled = false;
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
                if (ViewModel?.InvokeItem(item) == ContextKeybindingResult.Hide)
                {
                    WeakReferenceMessenger.Default.Send<CloseContextMenuMessage>();
                    WeakReferenceMessenger.Default.Send<FocusSearchBoxMessage>();
                }
                else
                {
                    UpdateUiForStackChange();
                }

                e.Handled = true;
            }
        }
        else if (e.Key == VirtualKey.Escape ||
            (e.Key == VirtualKey.Left && altPressed))
        {
            WeakReferenceMessenger.Default.Send<CloseContextMenuMessage>();
            UpdateUiForStackChange();

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

    public void UpdateUiForStackChange()
    {
        ContextFilterBox.Text = string.Empty;
        ViewModel?.SetSearchText(string.Empty);
        CommandsDropdown.SelectedIndex = 0;
        ContextFilterBox.Focus(FocusState.Programmatic);
    }
}
