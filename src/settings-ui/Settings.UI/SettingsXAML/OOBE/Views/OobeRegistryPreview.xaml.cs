// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.OOBE.Enums;
using Microsoft.PowerToys.Settings.UI.OOBE.ViewModel;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.PowerToys.Settings.UI.OOBE.Views
{
    public sealed partial class OobeRegistryPreview : Page
    {
        public OobePowerToysModule ViewModel { get; set; }

        public OobeRegistryPreview()
        {
            this.InitializeComponent();
            ViewModel = App.OobeShellViewModel.GetModule(PowerToysModules.RegistryPreview);
            DataContext = ViewModel;
        }

        private void Launch_RegistryPreview_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            ShellPage.SendDefaultIPCMessage("{\"action\":{\"RegistryPreview\":{\"action_name\":\"Launch\", \"value\":\"\"}}}");
        }

        private void SettingsLaunchButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (OobeWindow.OpenMainWindowCallback != null)
            {
                OobeWindow.OpenMainWindowCallback(typeof(RegistryPreviewPage));
            }

            ViewModel.LogOpeningSettingsEvent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.LogOpeningModuleEvent();

            // Disable the Launch button if the module is disabled
            var generalSettings = SettingsRepository<GeneralSettings>.GetInstance(SettingsUtils.Default).SettingsConfig;
            LaunchButton.IsEnabled = ModuleHelper.GetIsModuleEnabled(generalSettings, ManagedCommon.ModuleType.RegistryPreview);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.LogClosingModuleEvent();
        }
    }
}
