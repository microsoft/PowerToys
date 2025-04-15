// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Threading;

using global::Windows.System;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Controls;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Telemetry.Events;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using PowerToys.Interop;
using WinUIEx;

namespace Microsoft.PowerToys.Settings.UI.Flyout
{
    public sealed partial class LaunchPage : Page
    {
        private LauncherViewModel ViewModel { get; set; }

        public LaunchPage()
        {
            this.InitializeComponent();
            var settingsUtils = new SettingsUtils();
            ViewModel = new LauncherViewModel(SettingsRepository<GeneralSettings>.GetInstance(settingsUtils), Views.ShellPage.SendDefaultIPCMessage);
            DataContext = ViewModel;
        }

        private void ModuleButton_Click(object sender, RoutedEventArgs e)
        {
            FlyoutMenuButton selectedModuleBtn = sender as FlyoutMenuButton;
            bool moduleRun = true;

            // Closing manually the flyout to workaround focus gain problems
            App.GetFlyoutWindow()?.Hide();

            switch ((ModuleType)selectedModuleBtn.Tag)
            {
                case ModuleType.ColorPicker: // Launch ColorPicker
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowColorPickerSharedEvent()))
                    {
                        eventHandle.Set();
                    }

                    break;
                case ModuleType.EnvironmentVariables: // Launch Environment Variables
                    {
                        bool launchAdmin = SettingsRepository<EnvironmentVariablesSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.LaunchAdministrator;
                        string eventName = !App.IsElevated && launchAdmin
                            ? Constants.ShowEnvironmentVariablesAdminSharedEvent()
                            : Constants.ShowEnvironmentVariablesSharedEvent();

                        using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName))
                        {
                            eventHandle.Set();
                        }
                    }

                    break;

                case ModuleType.FancyZones: // Launch FancyZones Editor
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.FZEToggleEvent()))
                    {
                        eventHandle.Set();
                    }

                    break;

                case ModuleType.Hosts: // Launch Hosts
                    {
                        bool launchAdmin = SettingsRepository<HostsSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.LaunchAdministrator;
                        string eventName = !App.IsElevated && launchAdmin
                            ? Constants.ShowHostsAdminSharedEvent()
                            : Constants.ShowHostsSharedEvent();

                        using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName))
                        {
                            eventHandle.Set();
                        }
                    }

                    break;

                case ModuleType.RegistryPreview: // Launch Registry Preview
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.RegistryPreviewTriggerEvent()))
                    {
                        eventHandle.Set();
                    }

                    break;
                case ModuleType.MeasureTool: // Launch Screen Ruler
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.MeasureToolTriggerEvent()))
                    {
                        eventHandle.Set();
                    }

                    break;

                case ModuleType.PowerLauncher: // Launch Run
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.PowerLauncherSharedEvent()))
                    {
                        eventHandle.Set();
                    }

                    break;

                case ModuleType.PowerOCR: // Launch Text Extractor
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowPowerOCRSharedEvent()))
                    {
                        eventHandle.Set();
                    }

                    break;

                case ModuleType.Workspaces: // Launch Workspaces Editor
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.WorkspacesLaunchEditorEvent()))
                    {
                        eventHandle.Set();
                    }

                    break;

                case ModuleType.ShortcutGuide: // Launch Shortcut Guide
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShortcutGuideTriggerEvent()))
                    {
                        eventHandle.Set();
                    }

                    break;

                case ModuleType.CmdPal: // Show CmdPal
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowCmdPalEvent()))
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
                PowerToysTelemetry.Log.WriteEvent(new TrayFlyoutModuleRunEvent() { ModuleName = ((ModuleType)selectedModuleBtn.Tag).ToString() });
            }
        }

        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            App.OpenSettingsWindow(null, true);
        }

        private async void DocsBtn_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("https://aka.ms/PowerToysOverview"));
        }

        private void AllAppButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(AppsListPage), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
        }

        private void QuitButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.KillRunner();
            this.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
            {
                Application.Current.Exit();
            });
        }

        private void ReportBugBtn_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.StartBugReport();

            // Closing manually the flyout since no window will steal the focus
            App.GetFlyoutWindow()?.Hide();
        }
    }
}
