// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class ContextMenu : UserControl,
    IRecipient<OpenContextMenuMessage>,
    IRecipient<UpdateCommandBarMessage>,
    IRecipient<TryCommandKeybindingMessage>
{
    public ContextMenuViewModel ViewModel { get; } = new();

    public ContextMenu()
    {
        this.InitializeComponent();

        // RegisterAll isn't AOT compatible
        WeakReferenceMessenger.Default.Register<OpenContextMenuMessage>(this);
        WeakReferenceMessenger.Default.Register<UpdateCommandBarMessage>(this);
        WeakReferenceMessenger.Default.Register<TryCommandKeybindingMessage>(this);

        if (ViewModel is not null)
        {
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    public void Receive(OpenContextMenuMessage message)
    {
        ViewModel.FilterOnTop = message.ContextMenuFilterLocation == ContextMenuFilterLocation.Top;
        ViewModel.ResetContextMenu();

        UpdateUiForStackChange();
    }

    public void Receive(UpdateCommandBarMessage message)
    {
        UpdateUiForStackChange();
    }

    public void Receive(TryCommandKeybindingMessage msg)
    {
        var result = ViewModel?.CheckKeybinding(msg.Ctrl, msg.Alt, msg.Shift, msg.Win, msg.Key);

        if (result == ContextKeybindingResult.Hide)
        {
            msg.Handled = true;
            WeakReferenceMessenger.Default.Send<CloseContextMenuMessage>();
            UpdateUiForStackChange();
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
            if (InvokeCommand(item) == ContextKeybindingResult.Hide)
            {
                WeakReferenceMessenger.Default.Send<CloseContextMenuMessage>();
            }

            UpdateUiForStackChange();
        }
    }

    private void CommandsDropdown_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
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

        if (result == ContextKeybindingResult.Hide)
        {
            e.Handled = true;
            WeakReferenceMessenger.Default.Send<CloseContextMenuMessage>();
            UpdateUiForStackChange();
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

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        var prop = e.PropertyName;

        if (prop == nameof(ContextMenuViewModel.FilteredItems))
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
                if (InvokeCommand(item) == ContextKeybindingResult.Hide)
                {
                    WeakReferenceMessenger.Default.Send<CloseContextMenuMessage>();
                }

                UpdateUiForStackChange();

                e.Handled = true;
            }
        }
        else if (e.Key == VirtualKey.Escape ||
            (e.Key == VirtualKey.Left && altPressed))
        {
            if (ViewModel.CanPopContextStack())
            {
                ViewModel.PopContextStack();
                UpdateUiForStackChange();
            }
            else
            {
                WeakReferenceMessenger.Default.Send<CloseContextMenuMessage>();
                WeakReferenceMessenger.Default.Send<FocusSearchBoxMessage>();
                UpdateUiForStackChange();
            }

            e.Handled = true;
        }
    }

    private void ContextFilterBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Up)
        {
            NavigateUp();

            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Down)
        {
            NavigateDown();

            e.Handled = true;
        }

        CommandsDropdown_PreviewKeyDown(sender, e);
    }

    private void NavigateUp()
    {
        var newIndex = CommandsDropdown.SelectedIndex;

        if (CommandsDropdown.SelectedIndex > 0)
        {
            newIndex--;

            while (
                newIndex >= 0 &&
                IsSeparator(CommandsDropdown.Items[newIndex]) &&
                newIndex != CommandsDropdown.SelectedIndex)
            {
                newIndex--;
            }

            if (newIndex < 0)
            {
                newIndex = CommandsDropdown.Items.Count - 1;

                while (
                    newIndex >= 0 &&
                    IsSeparator(CommandsDropdown.Items[newIndex]) &&
                    newIndex != CommandsDropdown.SelectedIndex)
                {
                    newIndex--;
                }
            }
        }
        else
        {
            newIndex = CommandsDropdown.Items.Count - 1;
        }

        CommandsDropdown.SelectedIndex = newIndex;
    }

    private void NavigateDown()
    {
        var newIndex = CommandsDropdown.SelectedIndex;

        if (CommandsDropdown.SelectedIndex == CommandsDropdown.Items.Count - 1)
        {
            newIndex = 0;
        }
        else
        {
            newIndex++;

            while (
                newIndex < CommandsDropdown.Items.Count &&
                IsSeparator(CommandsDropdown.Items[newIndex]) &&
                newIndex != CommandsDropdown.SelectedIndex)
            {
                newIndex++;
            }

            if (newIndex >= CommandsDropdown.Items.Count)
            {
                newIndex = 0;

                while (
                    newIndex < CommandsDropdown.Items.Count &&
                    IsSeparator(CommandsDropdown.Items[newIndex]) &&
                    newIndex != CommandsDropdown.SelectedIndex)
                {
                    newIndex++;
                }
            }
        }

        CommandsDropdown.SelectedIndex = newIndex;
    }

    private bool IsSeparator(object item)
    {
        return item is SeparatorContextItemViewModel;
    }

    private void UpdateUiForStackChange()
    {
        ContextFilterBox.Text = string.Empty;
        ViewModel?.SetSearchText(string.Empty);
        CommandsDropdown.SelectedIndex = 0;
    }

    /// <summary>
    /// Manually focuses our search box. This needs to be called after we're actually
    /// In the UI tree - if we're in a Flyout, that's not until Opened()
    /// </summary>
    internal void FocusSearchBox()
    {
        ContextFilterBox.Focus(FocusState.Programmatic);
    }

    private ContextKeybindingResult InvokeCommand(CommandItemViewModel command) => ViewModel.InvokeCommand(command);
}
