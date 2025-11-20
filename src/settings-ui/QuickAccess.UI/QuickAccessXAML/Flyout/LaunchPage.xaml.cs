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

    private void ModuleButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FlyoutMenuButton selectedModuleBtn)
        {
            return;
        }

        if (selectedModuleBtn.Tag is not ModuleType moduleType)
        {
            return;
        }

        bool moduleRun = true;

        switch (moduleType)
        {
            case ModuleType.ColorPicker:
                using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowColorPickerSharedEvent()))
                {
                    eventHandle.Set();
                }

                break;
            case ModuleType.EnvironmentVariables:
                {
                    bool launchAdmin = SettingsRepository<EnvironmentVariablesSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.LaunchAdministrator;
                    bool isElevated = _coordinator?.IsRunnerElevated ?? false;
                    string eventName = !isElevated && launchAdmin
                        ? Constants.ShowEnvironmentVariablesAdminSharedEvent()
                        : Constants.ShowEnvironmentVariablesSharedEvent();

                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName))
                    {
                        eventHandle.Set();
                    }
                }

                break;
            case ModuleType.FancyZones:
                using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.FZEToggleEvent()))
                {
                    eventHandle.Set();
                }

                break;
            case ModuleType.Hosts:
                {
                    bool launchAdmin = SettingsRepository<HostsSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.LaunchAdministrator;
                    bool isElevated = _coordinator?.IsRunnerElevated ?? false;
                    string eventName = !isElevated && launchAdmin
                        ? Constants.ShowHostsAdminSharedEvent()
                        : Constants.ShowHostsSharedEvent();

                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName))
                    {
                        eventHandle.Set();
                    }
                }

                break;
            case ModuleType.PowerLauncher:
                using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.PowerLauncherSharedEvent()))
                {
                    eventHandle.Set();
                }

                break;
            case ModuleType.PowerOCR:
                using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowPowerOCRSharedEvent()))
                {
                    eventHandle.Set();
                }

                break;
            case ModuleType.RegistryPreview:
                using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.RegistryPreviewTriggerEvent()))
                {
                    eventHandle.Set();
                }

                break;
            case ModuleType.MeasureTool:
                using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.MeasureToolTriggerEvent()))
                {
                    eventHandle.Set();
                }

                break;
            case ModuleType.ShortcutGuide:
                using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShortcutGuideTriggerEvent()))
                {
                    eventHandle.Set();
                }

                break;
            case ModuleType.CmdPal:
                using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowCmdPalEvent()))
                {
                    eventHandle.Set();
                }

                break;
            case ModuleType.Workspaces:
                using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.WorkspacesLaunchEditorEvent()))
                {
                    eventHandle.Set();
                }

                break;
            default:
                moduleRun = false;
                break;
        }

        if (moduleRun)
        {
            _coordinator?.OnModuleLaunched(moduleType);
        }

        _coordinator?.HideFlyout();
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

    private void ReportBugBtn_Click(object sender, RoutedEventArgs e)
    {
    }

    private void UpdateInfoBar_Tapped(object sender, TappedRoutedEventArgs e)
    {
        _coordinator?.OpenGeneralSettingsForUpdates();
    }
}
