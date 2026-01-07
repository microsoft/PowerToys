// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.PowerToys.QuickAccess.ViewModels;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.PowerToys.QuickAccess.Flyout;

public sealed partial class AppsListPage : Page
{
    private FlyoutNavigationContext? _context;

    public AppsListPage()
    {
        InitializeComponent();
    }

    public AllAppsViewModel ViewModel { get; private set; } = default!;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is FlyoutNavigationContext context)
        {
            _context = context;
            ViewModel = context.AllAppsViewModel;
            DataContext = ViewModel;
            ViewModel.RefreshSettings();
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (_context == null || Frame == null)
        {
            return;
        }

        Frame.Navigate(typeof(LaunchPage), _context, new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromLeft });
    }

    private void SortAlphabetical_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.DashboardSortOrder = DashboardSortOrder.Alphabetical;
        }
    }

    private void SortByStatus_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.DashboardSortOrder = DashboardSortOrder.ByStatus;
        }
    }
}
