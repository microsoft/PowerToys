// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.OOBE.Enums;
using Microsoft.PowerToys.Settings.UI.OOBE.ViewModel;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using PowerToys.Interop;

namespace Microsoft.PowerToys.Settings.UI.OOBE.Views
{
    public sealed partial class OobeHosts : Page
    {
        public OobePowerToysModule ViewModel { get; }

        public OobeHosts()
        {
            InitializeComponent();
            ViewModel = new OobePowerToysModule(OobeShellPage.OobeShellHandler.Modules[(int)PowerToysModules.Hosts]);
            DataContext = ViewModel;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.LogOpeningModuleEvent();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.LogClosingModuleEvent();
        }

        private void Launch_Hosts_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            bool launchAdmin = SettingsRepository<HostsSettings>.GetInstance(SettingsUtils.Default).SettingsConfig.Properties.LaunchAdministrator;
            try
            {
                if (!App.IsElevated && launchAdmin)
                {
                    Process.Start(new ProcessStartInfo()
                    {
                        FileName = Path.GetFullPath("WinUI3Apps\\PowerToys.Hosts.exe"),
                        Verb = "runas",
                        UseShellExecute = true,
                    });
                    return;
                }

                Process.Start("WinUI3Apps\\PowerToys.Hosts.exe");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[OobeHosts] Launch failed", ex);
            }
        }

        private void Launch_Settings_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (OobeShellPage.OpenMainWindowCallback != null)
            {
                OobeShellPage.OpenMainWindowCallback(typeof(HostsPage));
            }

            ViewModel.LogOpeningSettingsEvent();
        }
    }
}
