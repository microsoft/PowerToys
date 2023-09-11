// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.OOBE.Enums;
using Microsoft.PowerToys.Settings.UI.OOBE.ViewModel;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.PowerToys.Settings.UI.OOBE.Views
{
    public sealed partial class OobeCropAndLock : Page
    {
        public OobePowerToysModule ViewModel { get; set; }

        public OobeCropAndLock()
        {
            InitializeComponent();
            ViewModel = new OobePowerToysModule(OobeShellPage.OobeShellHandler.Modules[(int)PowerToysModules.CropAndLock]);
            DataContext = ViewModel;
        }

        private void SettingsLaunchButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (OobeShellPage.OpenMainWindowCallback != null)
            {
                OobeShellPage.OpenMainWindowCallback(typeof(CropAndLockPage));
            }

            ViewModel.LogOpeningSettingsEvent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.LogOpeningModuleEvent();
            ReparentHotkeyControl.Keys = SettingsRepository<CropAndLockSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.ReparentHotkey.Value.GetKeysList();
            ThumbnailHotkeyControl.Keys = SettingsRepository<CropAndLockSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.ThumbnailHotkey.Value.GetKeysList();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.LogClosingModuleEvent();
        }
    }
}
