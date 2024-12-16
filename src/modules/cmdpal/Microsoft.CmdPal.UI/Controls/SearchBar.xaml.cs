// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.Views;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using CoreVirtualKeyStates = Windows.UI.Core.CoreVirtualKeyStates;
using VirtualKey = Windows.System.VirtualKey;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class SearchBar : UserControl, ICurrentPageAware
{
    /// <summary>
    /// Gets the <see cref="DispatcherQueueTimer"/> that we create to track keyboard input and throttle/debounce before we make queries.
    /// </summary>
    private readonly DispatcherQueueTimer _debounceTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();

    public bool Nested { get; set; }

    public PageViewModel? CurrentPageViewModel
    {
        get => (PageViewModel?)GetValue(CurrentPageViewModelProperty);
        set => SetValue(CurrentPageViewModelProperty, value);
    }

    // Using a DependencyProperty as the backing store for CurrentPageViewModel.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty CurrentPageViewModelProperty =
        DependencyProperty.Register(nameof(CurrentPageViewModel), typeof(PageViewModel), typeof(SearchBar), new PropertyMetadata(null, OnCurrentPageViewModelChanged));

    private static void OnCurrentPageViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        //// TODO: If the Debounce timer hasn't fired, we may want to store the current Filter in the OldValue/prior VM, but we don't want that to go actually do work...

        if (d is SearchBar @this
            && e.NewValue is PageViewModel page)
        {
            // TODO: In some cases we probably want commands to clear a filter somewhere in the process, so we need to figure out when that is.
            @this.FilterBox.Text = page.Filter;
        }
    }

    public SearchBar()
    {
        this.InitializeComponent();
    }

    private void BackButton_Tapped(object sender, TappedRoutedEventArgs e) => WeakReferenceMessenger.Default.Send<NavigateBackMessage>();

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
            WeakReferenceMessenger.Default.Send<ActivateSelectedListItemMessage>();

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
                // Clear the search box
                FilterBox.Text = string.Empty;
            }

            e.Handled = true;
        }
    }

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // TODO: We could encapsulate this in a Behavior if we wanted to bind to the Filter property.
        _debounceTimer.Debounce(
            () =>
            {
                // TODO: Actually Plumb Filtering
                Debug.WriteLine($"Filter: {FilterBox.Text}");
                if (CurrentPageViewModel != null)
                {
                    CurrentPageViewModel.Filter = FilterBox.Text;
                }
            },
            //// Couldn't find a good recommendation/resource for value here.
            //// This seems like a useful testing site for typing times: https://keyboardtester.info/keyboard-latency-test/
            //// i.e. if another keyboard press comes in within 100ms of the last, we'll wait before we fire off the request
            interval: TimeSpan.FromMilliseconds(100),
            //// If we're not already waiting, and this is blanking out or the first character type, we'll start filtering immediately instead to appear more responsive and either clear the filter to get back home faster or at least chop to the first starting letter.
            immediate: FilterBox.Text.Length <= 1);
    }
}
