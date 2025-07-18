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
    IRecipient<UpdateCurrentFilterIdsMessage>,
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

        // If the new page is a ListViewModel we'll get an UpdateFiltersMessage
        // with that lists filters. However, if it's just a PageViewModel, we
        // need to let the FilterViewModel know to clear its filters/currentfilterids
        if (e.NewValue is PageViewModel page)
        {
            if (page is not ListViewModel list || !list.HasFilters)
            {
                WeakReferenceMessenger.Default.Send<UpdateFiltersMessage>(new([], [], false));
            }
        }
    }

    public FiltersViewModel ViewModel { get; } = new();

    public Filter()
    {
        this.InitializeComponent();

        WeakReferenceMessenger.Default.Register<UpdateCurrentFilterIdsMessage>(this);
    }

    public void Receive(UpdateCurrentFilterIdsMessage message)
    {
        if (ViewModel.MultipleSelectionsEnabled)
        {
            FiltersDropdown.SelectedItems.Clear();
            foreach (var item in ViewModel.SelectedFilters)
            {
                FiltersDropdown.SelectedItems.Add(item);
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
            var newItems = e.AddedItems.OfType<FilterItemViewModel>().Select(s => s.Name).ToArray();
            var removedItems = e.RemovedItems.OfType<FilterItemViewModel>().Select(s => s.Name).ToArray();

            ViewModel.UpdateCurrentFilterIds(newItems, removedItems);
        }
    }

    private void FiltersDropdown_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (CurrentPageViewModel is ListViewModel listViewModel &&
            e.ClickedItem is FilterItemViewModel filterItem)
        {
            if (!ViewModel.IsSelected(filterItem.Id))
            {
                ViewModel.SelectOne(filterItem.Id);
            }
        }

        FilterButton.Flyout.Hide();
    }
}
