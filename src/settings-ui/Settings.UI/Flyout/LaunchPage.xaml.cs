// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.ObjectModel;
using System.Threading;
using global::Windows.System;
using interop;
using Microsoft.PowerToys.Settings.UI.Controls;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Telemetry.Events;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

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
            switch ((string)selectedModuleBtn.Tag)
            {
                case "ColorPicker": // Launch ColorPicker
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowColorPickerSharedEvent()))
                    {
                        eventHandle.Set();
                    }

                    break;
                case "FancyZones": // Launch FancyZones Editor
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.FZEToggleEvent()))
                    {
                        eventHandle.Set();
                    }

                    break;

                case "Hosts": // Launch Hosts
                    {
                        bool launchAdmin = SettingsRepository<HostsSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.LaunchAdministrator;
                        var actionName = "Launch";
                        if (!App.IsElevated && launchAdmin)
                        {
                            actionName = "LaunchAdministrator";
                        }

                        Views.ShellPage.SendDefaultIPCMessage("{\"action\":{\"Hosts\":{\"action_name\":\"" + actionName + "\", \"value\":\"\"}}}");
                    }

                    break;

                case "MeasureTool": // Launch Screen Ruler
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.MeasureToolTriggerEvent()))
                    {
                        eventHandle.Set();
                    }

                    break;

                case "PowerLauncher": // Launch Run
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.PowerLauncherSharedEvent()))
                    {
                        eventHandle.Set();
                    }

                    break;

                case "PowerOCR": // Launch Text Extractor
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowPowerOCRSharedEvent()))
                    {
                        eventHandle.Set();
                    }

                    break;

                case "ShortcutGuide": // Launch Shortcut Guide
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShortcutGuideTriggerEvent()))
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
                PowerToysTelemetry.Log.WriteEvent(new TrayFlyoutModuleRunEvent() { ModuleName = (string)selectedModuleBtn.Tag });
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
        }
    }
}
