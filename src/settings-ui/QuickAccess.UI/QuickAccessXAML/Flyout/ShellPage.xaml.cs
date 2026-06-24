// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Microsoft.PowerToys.QuickAccess.Services;
using Microsoft.PowerToys.QuickAccess.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.PowerToys.QuickAccess.Flyout;

/// <summary>
/// Hosts the flyout navigation frame.
/// </summary>
public sealed partial class ShellPage : Page
{
    private LauncherViewModel? _launcherViewModel;
    private AllAppsViewModel? _allAppsViewModel;
    private IQuickAccessCoordinator? _coordinator;

    public ShellPage()
    {
        InitializeComponent();
        ContentFrame.NavigationFailed += ContentFrame_NavigationFailed;
    }

    public void Initialize(IQuickAccessCoordinator coordinator, LauncherViewModel launcherViewModel, AllAppsViewModel allAppsViewModel)
    {
        _coordinator = coordinator;
        _launcherViewModel = launcherViewModel;
        _allAppsViewModel = allAppsViewModel;
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (_launcherViewModel == null || _allAppsViewModel == null || _coordinator == null)
        {
            return;
        }

        if (ContentFrame.Content is LaunchPage)
        {
            return;
        }

        var context = new FlyoutNavigationContext(_launcherViewModel, _allAppsViewModel, _coordinator);
        ContentFrame.Navigate(typeof(LaunchPage), context, new SuppressNavigationTransitionInfo());
    }

    internal void NavigateToLaunch()
    {
        if (_launcherViewModel == null || _allAppsViewModel == null || _coordinator == null)
        {
            return;
        }

        var context = new FlyoutNavigationContext(_launcherViewModel, _allAppsViewModel, _coordinator);
        ContentFrame.Navigate(typeof(LaunchPage), context, new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromLeft });
    }

    internal void RefreshIfAppsList()
    {
        if (ContentFrame.Content is AppsListPage appsListPage)
        {
            appsListPage.ViewModel?.RefreshSettings();
        }
    }

    private static void ContentFrame_NavigationFailed(object sender, NavigationFailedEventArgs e)
    {
        // A page constructor or XAML load failure here would otherwise bubble out of the
        // Frame and crash the launcher. Log the failure and mark it handled so the flyout
        // can remain available; the next summon will retry navigation.
        Logger.LogError($"QuickAccess: navigation to '{e.SourcePageType?.FullName}' failed.", e.Exception);
        e.Handled = true;
    }
}
