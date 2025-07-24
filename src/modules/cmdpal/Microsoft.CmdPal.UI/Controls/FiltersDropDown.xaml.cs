// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CmdPal.UI.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class FiltersDropDown : UserControl,
    ICurrentPageAware
{
    public PageViewModel? CurrentPageViewModel
    {
        get => (PageViewModel?)GetValue(CurrentPageViewModelProperty);
        set => SetValue(CurrentPageViewModelProperty, value);
    }

    // Using a DependencyProperty as the backing store for CurrentPageViewModel.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty CurrentPageViewModelProperty =
        DependencyProperty.Register(nameof(CurrentPageViewModel), typeof(PageViewModel), typeof(FiltersDropDown), new PropertyMetadata(null, OnCurrentPageViewModelChanged));

    public FiltersViewModel? ViewModel { get; set; }

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
                @this.UpdateFilters();
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

    public FiltersDropDown()
    {
        this.InitializeComponent();
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
                UpdateFilters();
            }
        }
    }

    private void FiltersComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CurrentPageViewModel is ListViewModel listViewModel &&
            FiltersComboBox.SelectedItem is FilterItemViewModel filterItem)
        {
            listViewModel.UpdateCurrentFilter(filterItem.Id);
        }

        // TODO: We need to handle a weird case where ComboBox will allow
        // separators to be selected (even thought their IsEnabled is false).
        // This doesn't happen once the ComboBox has been opened, but if the user
        // is using a keyboard to navigate the ComboBox, the enabled state of the
        // separator isn't respected.
    }

    private void UpdateFilters()
    {
        if (ViewModel is not null &&
            ViewModel.ShouldShowFilters)
        {
            FiltersComboBox.ItemsSource = ViewModel.Filters;
            FiltersComboBox.Visibility = Visibility.Visible;

            var selectedItem = ViewModel.Filters
                                        .OfType<FilterItemViewModel>()
                                        .FirstOrDefault(f => f.Id == ViewModel.CurrentFilterId);
            FiltersComboBox.SelectedItem = selectedItem;
        }
        else
        {
            FiltersComboBox.ItemsSource = null;
            FiltersComboBox.SelectedItem = null;
            FiltersComboBox.Visibility = Visibility.Collapsed;
        }
    }
}
