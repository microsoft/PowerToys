// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

public sealed partial class CommandBar : UserControl,
    IRecipient<OpenContextMenuMessage>,
    ICurrentPageAware
{
    public CommandBarViewModel ViewModel { get; set; } = new();

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
        CommandsDropdown.SelectedIndex = 0;
        CommandsDropdown.Focus(FocusState.Programmatic);
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
            ViewModel?.InvokeItemCommand.Execute(item);
            MoreCommandsButton.Flyout.Hide();
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

        if (ViewModel?.CheckKeybinding(ctrlPressed, altPressed, shiftPressed, winPressed, e.Key) ?? false)
        {
            e.Handled = true;
        }
    }
}
