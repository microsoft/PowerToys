// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using CoreVirtualKeyStates = Windows.UI.Core.CoreVirtualKeyStates;
using VirtualKey = Windows.System.VirtualKey;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class SearchBar : UserControl
{
    public bool Nested { get; set; }

    public SearchBar()
    {
        this.InitializeComponent();
    }

    private void BackButton_Tapped(object sender, TappedRoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.Send<NavigateBackMessage>();
    }

    private void FilterBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        var ctrlPressed = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
        if (e.Key == VirtualKey.Down)
        {
            WeakReferenceMessenger.Default.Send<NavigateNextCommand>();

            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Up)
        {
            WeakReferenceMessenger.Default.Send<NavigatePreviousCommand>();

            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Enter)
        {
            // TODO: ExecuteCommandMessage?
            e.Handled = true;
        } // ctrl+k
        else if (ctrlPressed && e.Key == VirtualKey.K)
        {
            // TODO: ShowActionsMessage?
            // Move code below to ActionBar
            /*FlyoutShowOptions options = new FlyoutShowOptions
            {
                ShowMode = FlyoutShowMode.Standard,
            };
            MoreCommandsButton.Flyout.ShowAt(MoreCommandsButton, options);
            ActionsDropdown.SelectedIndex = 0;
            ActionsDropdown.Focus(FocusState.Programmatic);*/
        }
        else if (e.Key == VirtualKey.Escape)
        {
            if (string.IsNullOrEmpty(FilterBox.Text))
            {
                WeakReferenceMessenger.Default.Send<NavigateBackMessage>();
            }
            else
            {
                // TODO: Clear the search box
            }

            e.Handled = true;
        }
    }

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // TODO
    }
}
