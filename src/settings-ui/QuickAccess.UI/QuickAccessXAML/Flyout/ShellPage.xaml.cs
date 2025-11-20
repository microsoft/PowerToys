// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.QuickAccess.Services;
using Microsoft.PowerToys.QuickAccess.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

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
}
