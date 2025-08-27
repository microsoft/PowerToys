// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.CmdPal.UI.Messages;
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
    IRecipient<UpdateSuggestionMessage>,
    ICurrentPageAware
{
    private readonly DispatcherQueue _queue = DispatcherQueue.GetForCurrentThread();

    /// <summary>
    /// Gets the <see cref="DispatcherQueueTimer"/> that we create to track keyboard input and throttle/debounce before we make queries.
    /// </summary>
    private readonly DispatcherQueueTimer _debounceTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
    private bool _isBackspaceHeld;

    private bool _inSuggestion;
    private string? _lastText;
    private string? _deletedSuggestion;

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

        if (@this is not null
            && e.OldValue is PageViewModel old)
        {
            old.PropertyChanged -= @this.Page_PropertyChanged;
        }

        if (@this is not null
            && e.NewValue is PageViewModel page)
        {
            // TODO: In some cases we probably want commands to clear a filter
            // somewhere in the process, so we need to figure out when that is.
            @this.FilterBox.Text = page.SearchTextBox;
            @this.FilterBox.Select(@this.FilterBox.Text.Length, 0);

            page.PropertyChanged += @this.Page_PropertyChanged;
        }
    }

    public SearchBar()
    {
        this.InitializeComponent();
        WeakReferenceMessenger.Default.Register<GoHomeMessage>(this);
        WeakReferenceMessenger.Default.Register<FocusSearchBoxMessage>(this);
        WeakReferenceMessenger.Default.Register<UpdateSuggestionMessage>(this);
    }

    public void ClearSearch()
    {
        // TODO GH #239 switch back when using the new MD text block
        // _ = _queue.EnqueueAsync(() =>
        _queue.TryEnqueue(new(() =>
        {
            this.FilterBox.Text = string.Empty;

            if (CurrentPageViewModel is not null)
            {
                CurrentPageViewModel.SearchTextBox = string.Empty;
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
        var shiftPressed = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);
        var winPressed = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.LeftWindows).HasFlag(CoreVirtualKeyStates.Down) ||
            InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.RightWindows).HasFlag(CoreVirtualKeyStates.Down);
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
            WeakReferenceMessenger.Default.Send<OpenContextMenuMessage>(new OpenContextMenuMessage(null, null, null, ContextMenuFilterLocation.Bottom));
            e.Handled = true;
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
                if (CurrentPageViewModel is not null)
                {
                    CurrentPageViewModel.SearchTextBox = FilterBox.Text;
                }
            }

            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Back)
        {
            // hack TODO GH #245
            if (CurrentPageViewModel is not null)
            {
                CurrentPageViewModel.SearchTextBox = FilterBox.Text;
            }
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
        else if (e.Key == VirtualKey.Right)
        {
            if (_inSuggestion)
            {
                _inSuggestion = false;
                _lastText = null;
                DoFilterBoxUpdate();
            }
        }
        else if (e.Key == VirtualKey.Down)
        {
            WeakReferenceMessenger.Default.Send<NavigateNextCommand>();

            e.Handled = true;
        }

        if (_inSuggestion)
        {
            if (
                 e.Key == VirtualKey.Back ||
                 e.Key == VirtualKey.Delete
                 )
            {
                _deletedSuggestion = FilterBox.Text;

                FilterBox.Text = _lastText ?? string.Empty;
                FilterBox.Select(FilterBox.Text.Length, 0);

                // Logger.LogInfo("deleting suggestion");
                _inSuggestion = false;
                _lastText = null;

                e.Handled = true;
                return;
            }

            var ignoreLeave =

                e.Key == VirtualKey.Up ||
                e.Key == VirtualKey.Down ||

                e.Key == VirtualKey.RightMenu ||
                e.Key == VirtualKey.LeftMenu ||
                e.Key == VirtualKey.Menu ||
                e.Key == VirtualKey.Shift ||
                e.Key == VirtualKey.RightShift ||
                e.Key == VirtualKey.LeftShift ||
                e.Key == VirtualKey.RightControl ||
                e.Key == VirtualKey.LeftControl ||
                e.Key == VirtualKey.Control;
            if (ignoreLeave)
            {
                return;
            }

            // Logger.LogInfo("leaving suggestion");
            _inSuggestion = false;
            _lastText = null;
        }

        if (!e.Handled)
        {
            var ctrlPressed = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
            var altPressed = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu).HasFlag(CoreVirtualKeyStates.Down);
            var shiftPressed = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);
            var winPressed = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.LeftWindows).HasFlag(CoreVirtualKeyStates.Down) ||
                InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.RightWindows).HasFlag(CoreVirtualKeyStates.Down);

            // The CommandBar is responsible for handling all the item keybindings,
            // since the bound context item may need to then show another
            // context menu
            TryCommandKeybindingMessage msg = new(ctrlPressed, altPressed, shiftPressed, winPressed, e.Key);
            WeakReferenceMessenger.Default.Send(msg);
            e.Handled = msg.Handled;
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
        // Logger.LogInfo($"FilterBox_TextChanged: {FilterBox.Text}");

        // TERRIBLE HACK TODO GH #245
        // There's weird wacky bugs with debounce currently. We're trying
        // to get them ingested, but while we wait for the toolkit feeds to
        // bubble, just manually send the first character, always
        // (otherwise aliases just stop working)
        if (FilterBox.Text.Length == 1)
        {
            DoFilterBoxUpdate();

            return;
        }

        if (_inSuggestion)
        {
            // Logger.LogInfo($"-- skipping, in suggestion --");
            return;
        }

        // TODO: We could encapsulate this in a Behavior if we wanted to bind to the Filter property.
        _debounceTimer.Debounce(
            () =>
            {
                DoFilterBoxUpdate();
            },
            //// Couldn't find a good recommendation/resource for value here. PT uses 50ms as default, so that is a reasonable default
            //// This seems like a useful testing site for typing times: https://keyboardtester.info/keyboard-latency-test/
            //// i.e. if another keyboard press comes in within 50ms of the last, we'll wait before we fire off the request
            interval: TimeSpan.FromMilliseconds(50),
            //// If we're not already waiting, and this is blanking out or the first character type, we'll start filtering immediately instead to appear more responsive and either clear the filter to get back home faster or at least chop to the first starting letter.
            immediate: FilterBox.Text.Length <= 1);
    }

    private void DoFilterBoxUpdate()
    {
        if (_inSuggestion)
        {
            // Logger.LogInfo($"--- skipping ---");
            return;
        }

        // Actually plumb Filtering to the view model
        if (CurrentPageViewModel is not null)
        {
            CurrentPageViewModel.SearchTextBox = FilterBox.Text;
        }
    }

    // Used to handle the case when a ListPage's `SearchText` may have changed
    private void Page_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        var property = e.PropertyName;

        if (CurrentPageViewModel is ListViewModel list)
        {
            if (property == nameof(ListViewModel.SearchText))
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
            else if (property == nameof(ListViewModel.InitialSearchText))
            {
                // GH #38712:
                // The ListPage will notify us of the `InitialSearchText` when
                // we first load the view model. We can use that as an
                // opportunity to immediately select the search text. That lets
                // the user start typing a new search without manually
                // selecting the old one.
                SelectSearch();
            }
        }
    }

    public void Receive(GoHomeMessage message) => ClearSearch();

    public void Receive(FocusSearchBoxMessage message) => FilterBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);

    public void Receive(UpdateSuggestionMessage message)
    {
        var suggestion = message.TextToSuggest;

        _queue.TryEnqueue(new(() =>
        {
            var clearSuggestion = string.IsNullOrEmpty(suggestion);

            if (clearSuggestion && _inSuggestion)
            {
                // Logger.LogInfo($"Cleared suggestion \"{_lastText}\" to {suggestion}");
                _inSuggestion = false;
                FilterBox.Text = _lastText ?? string.Empty;
                _lastText = null;
                return;
            }

            if (clearSuggestion)
            {
                _deletedSuggestion = null;
                return;
            }

            if (suggestion == _deletedSuggestion)
            {
                return;
            }
            else
            {
                _deletedSuggestion = null;
            }

            var currentText = _lastText ?? FilterBox.Text;

            _lastText = currentText;

            // if (_inSuggestion)
            // {
            //     Logger.LogInfo($"Suggestion from \"{_lastText}\" to {suggestion}");
            // }
            // else
            // {
            //     Logger.LogInfo($"Entering suggestion from \"{_lastText}\" to {suggestion}");
            // }
            _inSuggestion = true;

            var matchedChars = 0;
            var suggestionStartsWithQuote = suggestion.Length > 0 && suggestion[0] == '"';
            var currentStartsWithQuote = currentText.Length > 0 && currentText[0] == '"';
            var skipCheckingFirst = suggestionStartsWithQuote && !currentStartsWithQuote;
            for (int i = skipCheckingFirst ? 1 : 0, j = 0;
                 i < suggestion.Length && j < currentText.Length;
                 i++, j++)
            {
                if (string.Equals(
                    suggestion[i].ToString(),
                    currentText[j].ToString(),
                    StringComparison.OrdinalIgnoreCase))
                {
                    matchedChars++;
                }
                else
                {
                    break;
                }
            }

            var first = skipCheckingFirst ? "\"" : string.Empty;
            var second = currentText.AsSpan(0, matchedChars);
            var third = suggestion.AsSpan(matchedChars + (skipCheckingFirst ? 1 : 0));

            var newText = string.Concat(
                first,
                second,
                third);

            FilterBox.Text = newText;

            var wrappedInQuotes = suggestionStartsWithQuote && suggestion.Last() == '"';
            if (wrappedInQuotes)
            {
                FilterBox.Select(
                    (skipCheckingFirst ? 1 : 0) + matchedChars,
                    Math.Max(0, suggestion.Length - matchedChars - 1 + (skipCheckingFirst ? -1 : 0)));
            }
            else
            {
                FilterBox.Select(matchedChars, suggestion.Length - matchedChars);
            }
        }));
    }
}
