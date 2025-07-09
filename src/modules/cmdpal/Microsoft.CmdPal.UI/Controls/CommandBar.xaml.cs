// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.CmdPal.UI.Views;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class CommandBar : UserControl,
    IRecipient<OpenContextMenuMessage>,
    IRecipient<TryCommandKeybindingMessage>,
    ICurrentPageAware
{
    public CommandBarViewModel ViewModel { get; } = new();

    public PageViewModel? CurrentPageViewModel
    {
        get => (PageViewModel?)GetValue(CurrentPageViewModelProperty);
        set => SetValue(CurrentPageViewModelProperty, value);
    }

    // Using a DependencyProperty as the backing store for CurrentPage.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty CurrentPageViewModelProperty =
        DependencyProperty.Register(nameof(CurrentPageViewModel), typeof(PageViewModel), typeof(CommandBar), new PropertyMetadata(null));

    public CommandBar()
    {
        this.InitializeComponent();

        // RegisterAll isn't AOT compatible
        WeakReferenceMessenger.Default.Register<OpenContextMenuMessage>(this);
        WeakReferenceMessenger.Default.Register<TryCommandKeybindingMessage>(this);

        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    public void Receive(OpenContextMenuMessage message)
    {
        if (!ViewModel.ShouldShowContextMenu)
        {
            return;
        }

        var options = new FlyoutShowOptions
        {
            ShowMode = FlyoutShowMode.Standard,
        };
        MoreCommandsButton.Flyout.ShowAt(MoreCommandsButton, options);
        UpdateUiForStackChange();
    }

    public void Receive(TryCommandKeybindingMessage msg)
    {
        if (!ViewModel.ShouldShowContextMenu)
        {
            return;
        }

        var result = ViewModel?.CheckKeybinding(msg.Ctrl, msg.Alt, msg.Shift, msg.Win, msg.Key);

        if (result == ContextKeybindingResult.Hide)
        {
            msg.Handled = true;
        }
        else if (result == ContextKeybindingResult.KeepOpen)
        {
            if (!MoreCommandsButton.Flyout.IsOpen)
            {
                var options = new FlyoutShowOptions
                {
                    ShowMode = FlyoutShowMode.Standard,
                };
                MoreCommandsButton.Flyout.ShowAt(MoreCommandsButton, options);
            }

            UpdateUiForStackChange();

            msg.Handled = true;
        }
        else if (result == ContextKeybindingResult.Unhandled)
        {
            msg.Handled = false;
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "VS has a tendency to delete XAML bound methods over-aggressively")]
    private void PrimaryButton_Tapped(object sender, TappedRoutedEventArgs e)
    {
        ViewModel.InvokePrimaryCommand();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "VS has a tendency to delete XAML bound methods over-aggressively")]
    private void SecondaryButton_Tapped(object sender, TappedRoutedEventArgs e)
    {
        ViewModel.InvokeSecondaryCommand();
    }

    private void PageIcon_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (CurrentPageViewModel?.StatusMessages.Count > 0)
        {
            StatusMessagesFlyout.ShowAt(
                placementTarget: IconRoot,
                showOptions: new FlyoutShowOptions() { ShowMode = FlyoutShowMode.Standard });
        }
    }

    private void SettingsIcon_Tapped(object sender, TappedRoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.Send<OpenSettingsMessage>();
        e.Handled = true;
    }

    private void CommandsDropdown_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is CommandContextItemViewModel item)
        {
            if (ViewModel?.InvokeItem(item) == ContextKeybindingResult.Hide)
            {
                MoreCommandsButton.Flyout.Hide();
            }
            else
            {
                UpdateUiForStackChange();
            }
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
            MoreCommandsButton.Flyout.Hide();
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

    private void Flyout_Opened(object sender, object e)
    {
        UpdateUiForStackChange();
    }

    private void Flyout_Closing(FlyoutBase sender, FlyoutBaseClosingEventArgs args)
    {
        ViewModel?.ClearContextStack();
        WeakReferenceMessenger.Default.Send<FocusSearchBoxMessage>();
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        var prop = e.PropertyName;
        if (prop == nameof(ViewModel.ContextMenu))
        {
            UpdateUiForStackChange();
        }
    }

    private void ContextFilterBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel.ContextMenu?.SetSearchText(ContextFilterBox.Text);

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
                    MoreCommandsButton.Flyout.Hide();
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
            if (ViewModel.CanPopContextStack())
            {
                ViewModel.PopContextStack();
                UpdateUiForStackChange();
            }
            else
            {
                MoreCommandsButton.Flyout.Hide();
                WeakReferenceMessenger.Default.Send<FocusSearchBoxMessage>();
            }

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
        ViewModel.ContextMenu?.SetSearchText(string.Empty);
        CommandsDropdown.SelectedIndex = 0;
        ContextFilterBox.Focus(FocusState.Programmatic);
    }
}
