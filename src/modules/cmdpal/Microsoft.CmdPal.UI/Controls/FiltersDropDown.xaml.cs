// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using Windows.System;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class FiltersDropDown : UserControl,
    ICurrentPageAware
{
    private bool _isDropDownOpen;
    private string? _pendingSearchText;
    private IFilterItemViewModel[] _allItems = [];
    private FilterItemViewModel? _lastSelectedFilter;

    public PageViewModel? CurrentPageViewModel
    {
        get => (PageViewModel?)GetValue(CurrentPageViewModelProperty);
        set => SetValue(CurrentPageViewModelProperty, value);
    }

    public static readonly DependencyProperty CurrentPageViewModelProperty =
        DependencyProperty.Register(nameof(CurrentPageViewModel), typeof(PageViewModel), typeof(FiltersDropDown), new PropertyMetadata(null, OnCurrentPageViewModelChanged));

    private static void OnCurrentPageViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var @this = (FiltersDropDown)d;

        if (@this != null
            && e.OldValue is PageViewModel old)
        {
            old.PropertyChanged -= @this.Page_PropertyChanged;
        }

        // If this new page does not implement ListViewModel or if
        // it doesn't contain Filters, we need to clear any filters
        // that may have been set.
        if (@this != null)
        {
            if (e.NewValue is ListViewModel listViewModel)
            {
                @this.ViewModel = listViewModel.Filters;
            }
            else
            {
                @this.ViewModel = null;
            }
        }

        if (@this != null
            && e.NewValue is PageViewModel page)
        {
            page.PropertyChanged += @this.Page_PropertyChanged;
        }
    }

    public FiltersViewModel? ViewModel
    {
        get => (FiltersViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(FiltersViewModel), typeof(FiltersDropDown), new PropertyMetadata(null, OnViewModelChanged));

    /// <summary>
    /// Gets a value indicating whether the dropdown is currently open or the button has keyboard focus.
    /// </summary>
    public bool IsActive => _isDropDownOpen ||
        FilterDropDownButton.FocusState != FocusState.Unfocused;

    /// <summary>
    /// Gets a value indicating whether the filter control is visible (has filters to show).
    /// </summary>
    public bool IsFilterVisible => ViewModel?.ShouldShowFilters ?? false;

    private static readonly string _defaultFilterText = ResourceLoaderInstance.GetString("FiltersDropDown_DefaultText");

    public FiltersDropDown()
    {
        this.InitializeComponent();
        SelectedFilterText.Text = _defaultFilterText;
        FilterDropDownButton.AddHandler(
            CharacterReceivedEvent,
            new TypedEventHandler<UIElement, CharacterReceivedRoutedEventArgs>(FilterDropDownButton_CharacterReceived),
            true);
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FiltersDropDown @this)
        {
            return;
        }

        if (e.OldValue is FiltersViewModel oldVm)
        {
            oldVm.PropertyChanged -= @this.ViewModel_PropertyChanged;
        }

        if (e.NewValue is FiltersViewModel newVm)
        {
            newVm.PropertyChanged += @this.ViewModel_PropertyChanged;
        }

        @this.OnFiltersChanged();
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(FiltersViewModel.Filters)
            or nameof(FiltersViewModel.CurrentFilter)
            or nameof(FiltersViewModel.ShouldShowFilters))
        {
            OnFiltersChanged();
        }
    }

    private void OnFiltersChanged()
    {
        _allItems = ViewModel?.Filters ?? [];
        UpdateFilteredList();
        UpdateSelectedFilterDisplay();
    }

    private void UpdateSelectedFilterDisplay()
    {
        if (ViewModel?.CurrentFilter is FilterItemViewModel filter)
        {
            SelectedFilterText.Text = filter.Name;
            SelectedFilterIcon.SourceKey = filter.Icon;
            SelectedFilterIcon.Visibility = Visibility.Visible;
        }
        else
        {
            SelectedFilterText.Text = _defaultFilterText;
            SelectedFilterIcon.SourceKey = null;
            SelectedFilterIcon.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateFilteredList()
    {
        if (FilterListView == null)
        {
            return;
        }

        var searchText = FilterSearchBox?.Text?.Trim() ?? string.Empty;

        IFilterItemViewModel[] filtered;
        if (string.IsNullOrEmpty(searchText))
        {
            filtered = _allItems;
        }
        else
        {
            var list = new List<IFilterItemViewModel>();
            foreach (var item in _allItems)
            {
                if (item is FilterItemViewModel filterItem &&
                    filterItem.Name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) > -1)
                {
                    list.Add(item);
                }
            }

            filtered = list.ToArray();
        }

        FilterListView.ItemsSource = filtered;

        var hasResults = filtered.Length > 0;
        FilterListView.Visibility = hasResults ? Visibility.Visible : Visibility.Collapsed;
        NoResultsText.Visibility = hasResults ? Visibility.Collapsed : Visibility.Visible;

        // Restore selection to current filter if present
        if (_lastSelectedFilter != null && Array.IndexOf(filtered, _lastSelectedFilter) >= 0)
        {
            FilterListView.SelectedItem = _lastSelectedFilter;
        }
        else if (ViewModel?.CurrentFilter != null && Array.IndexOf(filtered, ViewModel.CurrentFilter) >= 0)
        {
            FilterListView.SelectedItem = ViewModel.CurrentFilter;
        }
        else if (hasResults)
        {
            // Select the first non-separator item
            IFilterItemViewModel? first = null;
            foreach (var item in filtered)
            {
                if (item is not SeparatorViewModel)
                {
                    first = item;
                    break;
                }
            }

            if (first != null)
            {
                FilterListView.SelectedItem = first;
            }
        }
    }

    // Used to handle the case when a ListPage's `Filters` may have changed
    private void Page_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        var property = e.PropertyName;

        if (CurrentPageViewModel is ListViewModel list)
        {
            if (property == nameof(ListViewModel.Filters))
            {
                ViewModel = list.Filters;
            }
        }
    }

    private void FilterDropDownButton_CharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs args)
    {
        // Redirect printable (non-space) characters to open flyout and type into search
        if (!char.IsControl(args.Character) && args.Character != ' ')
        {
            OpenFlyoutAndType(args.Character.ToString());
            args.Handled = true;
        }
    }

    private void FilterDropDownButton_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var modifiers = KeyModifiers.GetCurrent();

        switch (e.Key)
        {
            case VirtualKey.Down when modifiers.OnlyAlt:
                goto case VirtualKey.F4;

            case VirtualKey.Down or VirtualKey.Up:
            {
                if (!_isDropDownOpen)
                {
                    FilterFlyout.ShowAt(FilterDropDownButton);
                }

                if (e.Key == VirtualKey.Down)
                {
                    NavigateDown();
                }
                else
                {
                    NavigateUp();
                }

                e.Handled = true;
                break;
            }

            case VirtualKey.F4:
            {
                if (!_isDropDownOpen)
                {
                    FilterFlyout.ShowAt(FilterDropDownButton);
                }

                e.Handled = true;
                break;
            }
        }
    }

    private void FilterSearchBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var modifiers = KeyModifiers.GetCurrent();

        switch (e.Key)
        {
            case VirtualKey.Down:
                NavigateDown();
                e.Handled = true;
                break;
            case VirtualKey.Up:
                NavigateUp();
                e.Handled = true;
                break;
            case VirtualKey.Enter:
                SelectCurrentAndClose();
                e.Handled = true;
                break;
            case VirtualKey.Escape:
                if (!string.IsNullOrEmpty(FilterSearchBox.Text))
                {
                    FilterSearchBox.Text = string.Empty;
                }
                else
                {
                    CloseDropDownAndFocusSearch();
                }

                e.Handled = true;
                break;
            case VirtualKey.F when modifiers.Alt:
                CloseDropDownAndFocusSearch();
                e.Handled = true;
                break;
        }
    }

    private void FilterSearchBox_TextChanged(object sender, TextChangedEventArgs e) =>
        UpdateFilteredList();

    private void FilterListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is FilterItemViewModel filterItem)
        {
            SelectFilter(filterItem);
            CloseDropDownAndFocusSearch();
        }
    }

    private void FilterFlyout_Opened(object sender, object e)
    {
        _isDropDownOpen = true;

        FilterSearchBox.Text = _pendingSearchText ?? string.Empty;
        FilterSearchBox.SelectionStart = FilterSearchBox.Text.Length;
        _pendingSearchText = null;

        UpdateFilteredList();
        FilterSearchBox.Focus(FocusState.Programmatic);
    }

    private void FilterFlyout_Closed(object sender, object e)
    {
        _isDropDownOpen = false;
        _pendingSearchText = null;
        FilterSearchBox.Text = string.Empty;
    }

    private void OpenFlyoutAndType(string text)
    {
        _pendingSearchText = (_pendingSearchText ?? string.Empty) + text;
        if (!_isDropDownOpen)
        {
            FilterFlyout.ShowAt(FilterDropDownButton);
        }
        else
        {
            FilterSearchBox.Text = _pendingSearchText;
            FilterSearchBox.SelectionStart = FilterSearchBox.Text.Length;
            FilterSearchBox.Focus(FocusState.Programmatic);
            _pendingSearchText = null;
        }
    }

    /// <summary>
    /// Opens the filter dropdown flyout.
    /// </summary>
    public void OpenDropDown()
    {
        if (!_isDropDownOpen)
        {
            FilterFlyout.ShowAt(FilterDropDownButton);
        }
    }

    /// <summary>
    /// Closes the filter dropdown flyout and returns focus to the main search box.
    /// </summary>
    public void CloseDropDownAndFocusSearch()
    {
        if (_isDropDownOpen)
        {
            FilterFlyout.Hide();
        }

        WeakReferenceMessenger.Default.Send<FocusSearchBoxMessage>();
    }

    /// <summary>
    /// Closes the filter dropdown flyout.
    /// </summary>
    public void CloseDropDown()
    {
        if (_isDropDownOpen)
        {
            FilterFlyout.Hide();
        }

        FilterDropDownButton.Focus(FocusState.Programmatic);
    }

    /// <summary>
    /// Moves focus to this control (the dropdown button).
    /// </summary>
    public void FocusControl()
    {
        FilterDropDownButton.Focus(FocusState.Programmatic);
    }

    private void SelectCurrentAndClose()
    {
        if (FilterListView.SelectedItem is FilterItemViewModel filterItem)
        {
            SelectFilter(filterItem);
        }

        CloseDropDownAndFocusSearch();
    }

    private void SelectFilter(FilterItemViewModel filterItem)
    {
        _lastSelectedFilter = filterItem;

        if (CurrentPageViewModel is ListViewModel listViewModel)
        {
            listViewModel.UpdateCurrentFilter(filterItem.Id);
        }

        // Update display immediately (UpdateCurrentFilter is async)
        SelectedFilterText.Text = filterItem.Name;
        SelectedFilterIcon.SourceKey = filterItem.Icon;
        SelectedFilterIcon.Visibility = Visibility.Visible;
    }

    private void NavigateUp()
    {
        if (FilterListView.ItemsSource is not IFilterItemViewModel[] items || items.Length == 0)
        {
            return;
        }

        if (!HasSelectableItem(items))
        {
            return;
        }

        var newIndex = FilterListView.SelectedIndex;

        if (newIndex > 0)
        {
            newIndex--;

            while (newIndex >= 0 && IsSeparator(items[newIndex]))
            {
                newIndex--;
            }

            if (newIndex < 0)
            {
                newIndex = items.Length - 1;
                while (newIndex >= 0 && IsSeparator(items[newIndex]))
                {
                    newIndex--;
                }
            }
        }
        else
        {
            newIndex = items.Length - 1;
            while (newIndex >= 0 && IsSeparator(items[newIndex]))
            {
                newIndex--;
            }
        }

        if (newIndex >= 0)
        {
            FilterListView.SelectedIndex = newIndex;
            FilterListView.ScrollIntoView(FilterListView.SelectedItem);
        }
    }

    private void NavigateDown()
    {
        if (FilterListView.ItemsSource is not IFilterItemViewModel[] items || items.Length == 0)
        {
            return;
        }

        if (!HasSelectableItem(items))
        {
            return;
        }

        var newIndex = FilterListView.SelectedIndex;

        if (newIndex >= items.Length - 1)
        {
            newIndex = 0;
        }
        else
        {
            newIndex++;

            while (newIndex < items.Length && IsSeparator(items[newIndex]))
            {
                newIndex++;
            }

            if (newIndex >= items.Length)
            {
                newIndex = 0;
                while (newIndex < items.Length && IsSeparator(items[newIndex]))
                {
                    newIndex++;
                }
            }
        }

        if (newIndex < items.Length)
        {
            FilterListView.SelectedIndex = newIndex;
            FilterListView.ScrollIntoView(FilterListView.SelectedItem);
        }
    }

    private static bool IsSeparator(object item) => item is SeparatorViewModel;

    private static bool HasSelectableItem(IFilterItemViewModel[] items)
    {
        foreach (var item in items)
        {
            if (!IsSeparator(item))
            {
                return true;
            }
        }

        return false;
    }
}
