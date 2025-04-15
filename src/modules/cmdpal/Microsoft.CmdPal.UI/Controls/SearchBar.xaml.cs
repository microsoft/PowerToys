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

public sealed partial class SearchBar : UserControl,
    IRecipient<GoHomeMessage>,
    IRecipient<FocusSearchBoxMessage>,
    ICurrentPageAware
{
    private readonly DispatcherQueue _queue = DispatcherQueue.GetForCurrentThread();

    /// <summary>
    /// Gets the <see cref="DispatcherQueueTimer"/> that we create to track keyboard input and throttle/debounce before we make queries.
    /// </summary>
    private readonly DispatcherQueueTimer _debounceTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
    private bool _isBackspaceHeld;

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
        var @this = (SearchBar)d;

        if (@this != null
            && e.OldValue is PageViewModel old)
        {
            old.PropertyChanged -= @this.Page_PropertyChanged;
        }

        if (@this != null
            && e.NewValue is PageViewModel page)
        {
            // TODO: In some cases we probably want commands to clear a filter
            // somewhere in the process, so we need to figure out when that is.
            @this.FilterBox.Text = page.Filter;
            @this.FilterBox.Select(@this.FilterBox.Text.Length, 0);

            page.PropertyChanged += @this.Page_PropertyChanged;
        }
    }

    public SearchBar()
    {
        this.InitializeComponent();
        WeakReferenceMessenger.Default.Register<GoHomeMessage>(this);
        WeakReferenceMessenger.Default.Register<FocusSearchBoxMessage>(this);
    }

    public void ClearSearch()
    {
        // TODO GH #239 switch back when using the new MD text block
        // _ = _queue.EnqueueAsync(() =>
        _queue.TryEnqueue(new(() =>
        {
            this.FilterBox.Text = string.Empty;

            if (CurrentPageViewModel != null)
            {
                CurrentPageViewModel.Filter = string.Empty;
            }
        }));
    }

    public void SelectSearch()
    {
        // TODO GH #239 switch back when using the new MD text block
        // _ = _queue.EnqueueAsync(() =>
        _queue.TryEnqueue(new(() =>
        {
            this.FilterBox.SelectAll();
        }));
    }

    private void FilterBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        var ctrlPressed = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
        var altPressed = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu).HasFlag(CoreVirtualKeyStates.Down);

        if (ctrlPressed && e.Key == VirtualKey.Enter)
        {
            // ctrl+enter
            WeakReferenceMessenger.Default.Send<ActivateSecondaryCommandMessage>();
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Enter)
        {
            WeakReferenceMessenger.Default.Send<ActivateSelectedListItemMessage>();
            e.Handled = true;
        }
        else if (ctrlPressed && e.Key == VirtualKey.K)
        {
            // ctrl+k
            WeakReferenceMessenger.Default.Send<OpenContextMenuMessage>();
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Right)
        {
            if (CurrentPageViewModel != null && !string.IsNullOrEmpty(CurrentPageViewModel.TextToSuggest))
            {
                FilterBox.Text = CurrentPageViewModel.TextToSuggest;
                FilterBox.Select(FilterBox.Text.Length, 0);
                e.Handled = true;
            }
        }
        else if (e.Key == VirtualKey.Escape)
        {
            if (string.IsNullOrEmpty(FilterBox.Text))
            {
                WeakReferenceMessenger.Default.Send<NavigateBackMessage>(new());
            }
            else
            {
                // Clear the search box
                FilterBox.Text = string.Empty;

                // hack TODO GH #245
                if (CurrentPageViewModel != null)
                {
                    CurrentPageViewModel.Filter = FilterBox.Text;
                }
            }

            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Back)
        {
            // hack TODO GH #245
            if (CurrentPageViewModel != null)
            {
                CurrentPageViewModel.Filter = FilterBox.Text;
            }
        }
        else if (e.Key == VirtualKey.Left && altPressed)
        {
            WeakReferenceMessenger.Default.Send<NavigateBackMessage>(new());
        }
    }

    private void FilterBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Back)
        {
            if (string.IsNullOrEmpty(FilterBox.Text))
            {
                if (!_isBackspaceHeld)
                {
                    // Navigate back on single backspace when empty
                    WeakReferenceMessenger.Default.Send<NavigateBackMessage>(new(true));
                }

                e.Handled = true;
            }
            else
            {
                // Mark backspace as held to handle continuous deletion
                _isBackspaceHeld = true;
            }
        }
        else if (e.Key == VirtualKey.Up)
        {
            WeakReferenceMessenger.Default.Send<NavigatePreviousCommand>();

            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Down)
        {
            WeakReferenceMessenger.Default.Send<NavigateNextCommand>();

            e.Handled = true;
        }
    }

    private void FilterBox_PreviewKeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Back)
        {
            // Reset the backspace state on key release
            _isBackspaceHeld = false;
        }
    }

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        Debug.WriteLine($"FilterBox_TextChanged: {FilterBox.Text}");

        // TERRIBLE HACK TODO GH #245
        // There's weird wacky bugs with debounce currently. We're trying
        // to get them ingested, but while we wait for the toolkit feeds to
        // bubble, just manually send the first character, always
        // (otherwise aliases just stop working)
        if (FilterBox.Text.Length == 1)
        {
            if (CurrentPageViewModel != null)
            {
                CurrentPageViewModel.Filter = FilterBox.Text;
            }

            return;
        }

        // TODO: We could encapsulate this in a Behavior if we wanted to bind to the Filter property.
        _debounceTimer.Debounce(
            () =>
            {
                // Actually plumb Filtering to the viewmodel
                if (CurrentPageViewModel != null)
                {
                    CurrentPageViewModel.Filter = FilterBox.Text;
                }
            },
            //// Couldn't find a good recommendation/resource for value here. PT uses 50ms as default, so that is a reasonable default
            //// This seems like a useful testing site for typing times: https://keyboardtester.info/keyboard-latency-test/
            //// i.e. if another keyboard press comes in within 50ms of the last, we'll wait before we fire off the request
            interval: TimeSpan.FromMilliseconds(50),
            //// If we're not already waiting, and this is blanking out or the first character type, we'll start filtering immediately instead to appear more responsive and either clear the filter to get back home faster or at least chop to the first starting letter.
            immediate: FilterBox.Text.Length <= 1);
    }

    // Used to handle the case when a ListPage's `SearchText` may have changed
    private void Page_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        var property = e.PropertyName;
        if (CurrentPageViewModel is ListViewModel list &&
            property == nameof(ListViewModel.SearchText))
        {
            // Only if the text actually changed...
            // (sometimes this triggers on a round-trip of the SearchText)
            if (FilterBox.Text != list.SearchText)
            {
                // ... Update our displayed text, and...
                FilterBox.Text = list.SearchText;

                // ... Move the cursor to the end of the input
                FilterBox.Select(FilterBox.Text.Length, 0);
            }
        }
    }

    public void Receive(GoHomeMessage message) => ClearSearch();

    public void Receive(FocusSearchBoxMessage message) => this.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
}
