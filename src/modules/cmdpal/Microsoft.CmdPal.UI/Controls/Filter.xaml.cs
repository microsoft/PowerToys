// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
using Microsoft.Bot.AdaptiveExpressions.Core;
using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.CmdPal.UI.Views;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Diagnostics.Tracing.AutomatedAnalysis;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using CoreVirtualKeyStates = Windows.UI.Core.CoreVirtualKeyStates;
using VirtualKey = Windows.System.VirtualKey;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class Filter : UserControl,
    ICurrentPageAware
{
    public PageViewModel? CurrentPageViewModel
    {
        get => (PageViewModel?)GetValue(CurrentPageViewModelProperty);
        set => SetValue(CurrentPageViewModelProperty, value);
    }

    // Using a DependencyProperty as the backing store for CurrentPageViewModel.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty CurrentPageViewModelProperty =
        DependencyProperty.Register(nameof(CurrentPageViewModel), typeof(PageViewModel), typeof(Filter), new PropertyMetadata(null, OnCurrentPageViewModelChanged));

    private static void OnCurrentPageViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var @this = (Filter)d;

        if (@this != null
            && e.OldValue is PageViewModel old)
        {
            old.PropertyChanged -= @this.Page_PropertyChanged;
        }

        if (@this != null
            && e.NewValue is PageViewModel page)
        {
            page.PropertyChanged += @this.Page_PropertyChanged;
        }
    }

    public FiltersViewModel ViewModel { get; } = new();

    public Filter()
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
                WeakReferenceMessenger.Default.Send(new UpdateFiltersMessage(list.));
            }
        }
    }

    private void FilterButton_Click(object sender, RoutedEventArgs e)
    {
        FilterButton.Flyout.ShowAt(FilterButton);
    }

    private void FiltersDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CurrentPageViewModel is ListViewModel listViewModel)
        {
            var filterIds = FiltersDropdown.SelectedItems
                                            .OfType<FilterItemViewModel>()
                                            .Select(item => item.Id)
                                            .ToArray();

            listViewModel.UpdateCurrentFilterIds(ViewModel.CurrentFilterIds);
        }
    }

    private void FiltersDropdown_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (CurrentPageViewModel is ListViewModel listViewModel &&
            listViewModel.Filters is not null &&
            e.ClickedItem is FilterItemViewModel filterItem)
        {
            // If we can only select one, then go ahead and
            // send it to the page
            if (!ViewModel.MultipleSelectionsEnabled)
            {
                listViewModel.UpdateCurrentFilterIds([filterItem.Id]);
                FilterButton.Flyout.Hide();
            }
        }
    }
}
