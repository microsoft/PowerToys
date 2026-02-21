// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.OOBE.Enums;
using Microsoft.PowerToys.Settings.UI.OOBE.ViewModel;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using PowerToys.Interop;

namespace Microsoft.PowerToys.Settings.UI.OOBE.Views
{
    public sealed partial class OobeEnvironmentVariables : Page
    {
        public OobePowerToysModule ViewModel { get; }

        public OobeEnvironmentVariables()
        {
            InitializeComponent();
            ViewModel = App.OobeShellViewModel.GetModule(PowerToysModules.EnvironmentVariables);
            DataContext = ViewModel;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.LogOpeningModuleEvent();

            // Disable the Launch button if the module is disabled
            var generalSettings = SettingsRepository<GeneralSettings>.GetInstance(SettingsUtils.Default).SettingsConfig;
            LaunchButton.IsEnabled = ModuleHelper.GetIsModuleEnabled(generalSettings, ManagedCommon.ModuleType.EnvironmentVariables);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.LogClosingModuleEvent();
        }

        private void Launch_EnvironmentVariables_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            bool launchAdmin = SettingsRepository<EnvironmentVariablesSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.LaunchAdministrator;
            try
            {
                if (!App.IsElevated && launchAdmin)
                {
                    Process.Start(new ProcessStartInfo()
                    {
                        FileName = Path.GetFullPath("WinUI3Apps\\PowerToys.EnvironmentVariables.exe"),
                        Verb = "runas",
                        UseShellExecute = true,
                    });
                    return;
                }

                Process.Start("WinUI3Apps\\PowerToys.EnvironmentVariables.exe");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[EnvironmentVariablesViewModel] Launch failed", ex);
            }
        }

        private void Launch_Settings_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (OobeWindow.OpenMainWindowCallback != null)
            {
                OobeWindow.OpenMainWindowCallback(typeof(EnvironmentVariablesPage));
            }

            ViewModel.LogOpeningSettingsEvent();
        }
    }
}
