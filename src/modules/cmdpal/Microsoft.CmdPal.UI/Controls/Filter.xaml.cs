// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.CmdPal.UI.Views;
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

    public FiltersViewModel ViewModel
    {
        get => (FiltersViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    // Using a DependencyProperty as the backing store for ViewModel
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(FiltersViewModel), typeof(Filter), new PropertyMetadata(new FiltersViewModel()));

    private static void OnCurrentPageViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var @this = (Filter)d;

        if (@this != null)
        {
            if (e.NewValue is ListViewModel listViewModel)
            {
                @this.SetValue(ViewModelProperty, listViewModel.Filters);
            }
            else
            {
                @this.SetValue(ViewModelProperty, new FiltersViewModel());
            }
        }
    }

    public Filter()
    {
        this.InitializeComponent();
    }

    private void FiltersDropdown_ItemClick(object sender, ItemClickEventArgs e)
    {
    }

    private void FiltersDropdown_KeyDown(object sender, KeyRoutedEventArgs e)
    {
    }

    private void FilterButton_Tapped(object sender, TappedRoutedEventArgs e)
    {
        FilterButton.Flyout.ShowAt(FilterButton);
    }
}
