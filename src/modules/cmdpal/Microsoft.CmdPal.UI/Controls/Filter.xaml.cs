// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.CmdPal.UI.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class Filter : UserControl,
    IRecipient<UpdateCurrentFilterIdMessage>,
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
        // need to let the FilterViewModel know to clear its filters/current filter ids
        if (e.NewValue is PageViewModel page)
        {
            if (page is not ListViewModel list || !list.HasFilters)
            {
                WeakReferenceMessenger.Default.Send<UpdateFiltersMessage>(new([], string.Empty));
            }
        }
    }

    public FiltersViewModel ViewModel { get; } = new();

    public Filter()
    {
        this.InitializeComponent();

        WeakReferenceMessenger.Default.Register<UpdateCurrentFilterIdMessage>(this);
    }

    public void Receive(UpdateCurrentFilterIdMessage message)
    {
        if (!string.IsNullOrEmpty(message.CurrentFilterId) &&
            FiltersDropdown.SelectedItem is not null)
        {
            FiltersDropdown.SelectedItem = ViewModel.SelectedFilter;
        }
    }

    private void FiltersDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CurrentPageViewModel is ListViewModel listViewModel &&
            FiltersDropdown.SelectedItem is FilterItemViewModel filterItem)
        {
            WeakReferenceMessenger.Default.Send<UpdateCurrentFilterIdMessage>(new(filterItem.Id));
        }
    }
}
