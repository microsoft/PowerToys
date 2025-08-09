// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CmdPal.UI.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class FiltersDropDown : UserControl,
    ICurrentPageAware
{
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
        DependencyProperty.Register(nameof(ViewModel), typeof(FiltersViewModel), typeof(FiltersDropDown), new PropertyMetadata(null));

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
    }

    private void FiltersComboBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
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
        var newIndex = FiltersComboBox.SelectedIndex;

        if (FiltersComboBox.SelectedIndex > 0)
        {
            newIndex--;

            while (
                newIndex >= 0 &&
                IsSeparator(FiltersComboBox.Items[newIndex]) &&
                newIndex != FiltersComboBox.SelectedIndex)
            {
                newIndex--;
            }

            if (newIndex < 0)
            {
                newIndex = FiltersComboBox.Items.Count - 1;

                while (
                    newIndex >= 0 &&
                    IsSeparator(FiltersComboBox.Items[newIndex]) &&
                    newIndex != FiltersComboBox.SelectedIndex)
                {
                    newIndex--;
                }
            }
        }
        else
        {
            newIndex = FiltersComboBox.Items.Count - 1;
        }

        FiltersComboBox.SelectedIndex = newIndex;
    }

    private void NavigateDown()
    {
        var newIndex = FiltersComboBox.SelectedIndex;

        if (FiltersComboBox.SelectedIndex == FiltersComboBox.Items.Count - 1)
        {
            newIndex = 0;
        }
        else
        {
            newIndex++;

            while (
                newIndex < FiltersComboBox.Items.Count &&
                IsSeparator(FiltersComboBox.Items[newIndex]) &&
                newIndex != FiltersComboBox.SelectedIndex)
            {
                newIndex++;
            }

            if (newIndex >= FiltersComboBox.Items.Count)
            {
                newIndex = 0;

                while (
                    newIndex < FiltersComboBox.Items.Count &&
                    IsSeparator(FiltersComboBox.Items[newIndex]) &&
                    newIndex != FiltersComboBox.SelectedIndex)
                {
                    newIndex++;
                }
            }
        }

        FiltersComboBox.SelectedIndex = newIndex;
    }

    private bool IsSeparator(object item)
    {
        return item is SeparatorViewModel;
    }
}
