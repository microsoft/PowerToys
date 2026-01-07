// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using ManagedCommon;
using Microsoft.PowerToys.QuickAccess.Services;
using Microsoft.PowerToys.QuickAccess.ViewModels;
using Microsoft.PowerToys.Settings.UI.Controls;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using PowerToys.Interop;
using Windows.System;

namespace Microsoft.PowerToys.QuickAccess.Flyout;

public sealed partial class LaunchPage : Page
{
    private AllAppsViewModel? _allAppsViewModel;
    private IQuickAccessCoordinator? _coordinator;

    public LaunchPage()
    {
        InitializeComponent();
    }

    public LauncherViewModel ViewModel { get; private set; } = default!;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is FlyoutNavigationContext context)
        {
            ViewModel = context.LauncherViewModel;
            _allAppsViewModel = context.AllAppsViewModel;
            _coordinator = context.Coordinator;
            DataContext = ViewModel;
        }
    }

    private void SettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        _coordinator?.OpenSettings();
    }

    private async void DocsBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_coordinator == null || !await _coordinator.ShowDocumentationAsync())
        {
            await Launcher.LaunchUriAsync(new Uri("https://aka.ms/PowerToysOverview"));
        }
    }

    private void AllAppButton_Click(object sender, RoutedEventArgs e)
    {
        if (Frame == null || _allAppsViewModel == null || ViewModel == null || _coordinator == null)
        {
            return;
        }

        var context = new FlyoutNavigationContext(ViewModel, _allAppsViewModel, _coordinator);
        Frame.Navigate(typeof(AppsListPage), context, new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });
    }

    public void ReportBugBtn_Click(object sender, RoutedEventArgs e)
    {
        _coordinator?.ReportBug();
    }

    private void UpdateInfoBar_Tapped(object sender, TappedRoutedEventArgs e)
    {
        _coordinator?.OpenGeneralSettingsForUpdates();
    }
}
