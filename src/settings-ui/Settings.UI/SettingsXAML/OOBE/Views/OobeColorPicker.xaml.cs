// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.OOBE.Enums;
using Microsoft.PowerToys.Settings.UI.OOBE.ViewModel;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.PowerToys.Settings.UI.OOBE.Views
{
    public sealed partial class OobeColorPicker : Page
    {
        public OobePowerToysModule ViewModel { get; set; }

        public OobeColorPicker()
        {
            this.InitializeComponent();
            ViewModel = ViewModel = App.OobeShellViewModel.GetModule(PowerToysModules.ColorPicker);
            DataContext = ViewModel;
        }

        private void Start_ColorPicker_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (OobeWindow.ColorPickerSharedEventCallback != null)
            {
                using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, OobeWindow.ColorPickerSharedEventCallback()))
                {
                    eventHandle.Set();
                }
            }

            ViewModel.LogRunningModuleEvent();
        }

        private void SettingsLaunchButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (OobeWindow.OpenMainWindowCallback != null)
            {
                OobeWindow.OpenMainWindowCallback(typeof(ColorPickerPage));
            }

            ViewModel.LogOpeningSettingsEvent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.LogOpeningModuleEvent();
            ColorPickerSettings settings = SettingsUtils.Default.GetSettingsOrDefault<ColorPickerSettings, ColorPickerSettingsVersion1>(ColorPickerSettings.ModuleName, settingsUpgrader: ColorPickerSettings.UpgradeSettings);

            HotkeyControl.Keys = settings.Properties.ActivationShortcut.GetKeysList();

            // Disable the Launch button if the module is disabled
            var generalSettings = SettingsRepository<GeneralSettings>.GetInstance(SettingsUtils.Default).SettingsConfig;
            LaunchButton.IsEnabled = ModuleHelper.GetIsModuleEnabled(generalSettings, ManagedCommon.ModuleType.ColorPicker);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.LogClosingModuleEvent();
        }
    }
}
