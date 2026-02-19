// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.OOBE.Enums;
using Microsoft.PowerToys.Settings.UI.OOBE.ViewModel;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.PowerToys.Settings.UI.OOBE.Views
{
    public sealed partial class OobeAwake : Page
    {
        public OobePowerToysModule ViewModel { get; set; }

        public OobeAwake()
        {
            InitializeComponent();
            ViewModel = App.OobeShellViewModel.GetModule(PowerToysModules.Awake);
            DataContext = ViewModel;
        }

        private void SettingsLaunchButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (OobeWindow.OpenMainWindowCallback != null)
            {
                OobeWindow.OpenMainWindowCallback(typeof(AwakePage));
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
