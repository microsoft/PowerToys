// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using interop;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.OOBE.Enums;
using Microsoft.PowerToys.Settings.UI.OOBE.ViewModel;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.PowerToys.Settings.UI.OOBE.Views
{
    public sealed partial class OobeEnvironmentVariables : Page
    {
        public OobePowerToysModule ViewModel { get; }

        public OobeEnvironmentVariables()
        {
            InitializeComponent();
            ViewModel = new OobePowerToysModule(OobeShellPage.OobeShellHandler.Modules[(int)PowerToysModules.EnvironmentVariables]);
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

        private void Launch_EnvironmentVariables_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
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

        private void Launch_Settings_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (OobeShellPage.OpenMainWindowCallback != null)
            {
                OobeShellPage.OpenMainWindowCallback(typeof(EnvironmentVariablesPage));
            }

            ViewModel.LogOpeningSettingsEvent();
        }
    }
}
