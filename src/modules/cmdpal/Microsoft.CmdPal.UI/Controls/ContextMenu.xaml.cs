// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

using CommunityToolkit.Mvvm.Messaging;

using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CmdPal.Core.ViewModels.Messages;
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

        if (ViewModel != null)
        {
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        this.PreviewKeyDown += UserControl_KeyDown_Enhanced;
    }

    public void Receive(OpenContextMenuMessage message)
    {
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

        CommandsDropdown_KeyDown(sender, e);
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

    private ContextKeybindingResult InvokeCommand(CommandItemViewModel command) => ViewModel.InvokeCommand(command);

    /// <summary>
    /// Converts a VirtualKey to its corresponding character string,
    /// taking into account current keyboard state (Shift, Caps Lock, etc.)
    /// </summary>
    private static string? GetCharacterFromVirtualKey(VirtualKey key)
    {
        // Get current keyboard state
        var keyboardState = new byte[256];
        if (!GetKeyboardState(keyboardState))
        {
            return null;
        }

        // Convert VirtualKey to Windows virtual key code
        var virtualKeyCode = (uint)key;

        // Get scan code
        var scanCode = MapVirtualKey(virtualKeyCode, 0);

        // Convert to Unicode characters
        var buffer = new StringBuilder(5);
        var result = ToUnicode(
            virtualKeyCode,
            scanCode,
            keyboardState,
            buffer,
            buffer.Capacity,
            0);

        if (result > 0)
        {
            var character = buffer.ToString();

            // Filter out control characters and ensure it's printable
            if (IsPrintableCharacter(character))
            {
                return character;
            }
        }

        return null;
    }

    /// <summary>
    /// Alternative method using InputKeyboardSource for getting modifier states
    /// </summary>
    private static string? GetCharacterFromVirtualKeyAlternative(VirtualKey key)
    {
        // Get modifier states
        var shiftState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);
        var ctrlState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
        var altState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu);
        var capsLockState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.CapitalLock);

        var isShiftPressed = shiftState.HasFlag(CoreVirtualKeyStates.Down);
        var isCtrlPressed = ctrlState.HasFlag(CoreVirtualKeyStates.Down);
        var isAltPressed = altState.HasFlag(CoreVirtualKeyStates.Down);
        var isCapsLockOn = capsLockState.HasFlag(CoreVirtualKeyStates.Locked);

        // Don't handle if Ctrl is pressed (could be shortcuts)
        if (isCtrlPressed)
        {
            return null;
        }

        return ConvertVirtualKeyToChar(key, isShiftPressed, isCapsLockOn, isAltPressed);
    }

    /// <summary>
    /// Manual conversion for common keys (fallback method)
    /// </summary>
    private static string? ConvertVirtualKeyToChar(VirtualKey key, bool isShiftPressed, bool isCapsLockOn, bool isAltPressed)
    {
        // Letters
        if (key >= VirtualKey.A && key <= VirtualKey.Z)
        {
            var baseChar = (char)('a' + (key - VirtualKey.A));
            var shouldBeUppercase = isShiftPressed ^ isCapsLockOn; // XOR for proper caps lock behavior
            return shouldBeUppercase ? char.ToUpper(baseChar, CultureInfo.CurrentUICulture).ToString() : baseChar.ToString();
        }

        // Numbers and their shifted symbols
        if (key >= VirtualKey.Number0 && key <= VirtualKey.Number9)
        {
            if (isShiftPressed)
            {
                return key switch
                {
                    VirtualKey.Number1 => "!",
                    VirtualKey.Number2 => "@",
                    VirtualKey.Number3 => "#",
                    VirtualKey.Number4 => "$",
                    VirtualKey.Number5 => "%",
                    VirtualKey.Number6 => "^",
                    VirtualKey.Number7 => "&",
                    VirtualKey.Number8 => "*",
                    VirtualKey.Number9 => "(",
                    VirtualKey.Number0 => ")",
                    _ => null,
                };
            }
            else
            {
                return ((char)('0' + (key - VirtualKey.Number0))).ToString();
            }
        }

        // Numpad numbers
        if (key >= VirtualKey.NumberPad0 && key <= VirtualKey.NumberPad9)
        {
            return ((char)('0' + (key - VirtualKey.NumberPad0))).ToString();
        }

        // Special keys
        return key switch
        {
            VirtualKey.Space => " ",
            VirtualKey.Tab => "\t",

            // Numpad operators
            VirtualKey.Add => "+",
            VirtualKey.Subtract => "-",
            VirtualKey.Multiply => "*",
            VirtualKey.Divide => "/",
            VirtualKey.Decimal => ".",

            _ => null,
        };
    }

    /// <summary>
    /// Checks if a character is printable (not a control character)
    /// </summary>
    private static bool IsPrintableCharacter(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        foreach (var c in text)
        {
            // Check if character is printable
            // Control characters are in ranges 0x00-0x1F and 0x7F-0x9F
            if (char.IsControl(c))
            {
                return false;
            }

            // Additional check for common non-printable Unicode categories
            var category = char.GetUnicodeCategory(c);
            if (category == System.Globalization.UnicodeCategory.Control ||
                category == System.Globalization.UnicodeCategory.Format ||
                category == System.Globalization.UnicodeCategory.Surrogate ||
                category == System.Globalization.UnicodeCategory.PrivateUse ||
                category == System.Globalization.UnicodeCategory.OtherNotAssigned)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Enhanced version that also handles special navigation keys
    /// </summary>
    private void UserControl_KeyDown_Enhanced(object sender, KeyRoutedEventArgs e)
    {
        if (ContextFilterBox.FocusState != FocusState.Unfocused)
        {
            return;
        }

        var character = GetCharacterFromVirtualKey(e.Key);
        if (string.IsNullOrEmpty(character))
        {
            character = GetCharacterFromVirtualKeyAlternative(e.Key);
        }

        if (string.IsNullOrEmpty(character))
        {
            return;
        }

        ContextFilterBox.Focus(FocusState.Keyboard);

        // Insert character at current position
        var selectionStart = ContextFilterBox.SelectionStart;
        var selectionLength = ContextFilterBox.SelectionLength;
        var currentText = ContextFilterBox.Text ?? string.Empty;

        // Replace selection or insert at cursor
        if (selectionLength > 0)
        {
            currentText = currentText.Remove(selectionStart, selectionLength);
        }

        ContextFilterBox.Text = currentText.Insert(selectionStart, character);
        ContextFilterBox.SelectionStart = selectionStart + character.Length;
        ContextFilterBox.SelectionLength = 0;

        e.Handled = true;
    }

    [DllImport("user32.dll")]
    private static extern bool GetKeyboardState(byte[] lpKeyState);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll")]
    private static extern int ToUnicode(
        uint wVirtKey,
        uint wScanCode,
        byte[] lpKeyState,
        [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwszBuff,
        int cchBuff,
        uint wFlags);
}
