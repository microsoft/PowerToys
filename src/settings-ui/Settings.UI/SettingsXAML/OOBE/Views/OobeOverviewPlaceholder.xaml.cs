// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using AllExperiments;
using Microsoft.PowerToys.Settings.UI.OOBE.Enums;
using Microsoft.PowerToys.Settings.UI.OOBE.ViewModel;
using Microsoft.PowerToys.Settings.UI.Services;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.PowerToys.Settings.UI.OOBE.Views
{
    public sealed partial class OobeOverviewPlaceholder : Page
    {
        public OobePowerToysModule ViewModel { get; set; }

        public OobeOverviewPlaceholder()
        {
            this.InitializeComponent();
            ViewModel = new OobePowerToysModule(OobeShellPage.OobeShellHandler.Modules[(int)PowerToysModules.Overview]);
            DataContext = ViewModel;
        }

        private static async Task<bool> GetIsExperiment()
        {
            Experiments landingPageExp = new Experiments();
            var experimentEnabled = await landingPageExp.EnableLandingPageExperimentAsync();
            return experimentEnabled;
        }

        private async void Reload()
        {
            var isExperiment = await GetIsExperiment();

            if (isExperiment)
            {
                this.Frame.Navigate(typeof(OobeOverviewAlternate));
            }
            else
            {
                this.Frame.Navigate(typeof(OobeOverview));
            }
        }

        private void Page_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Reload();
        }

        private void SettingsLaunchButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (OobeShellPage.OpenMainWindowCallback != null)
            {
                OobeShellPage.OpenMainWindowCallback(typeof(DashboardPage));
            }

            ViewModel.LogOpeningSettingsEvent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.LogOpeningModuleEvent();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.LogClosingModuleEvent();
        }
    }
}
