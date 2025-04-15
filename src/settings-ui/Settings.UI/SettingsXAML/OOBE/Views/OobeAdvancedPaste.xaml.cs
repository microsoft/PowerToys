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
    public sealed partial class OobeAdvancedPaste : Page
    {
        public OobePowerToysModule ViewModel { get; set; }

        public OobeAdvancedPaste()
        {
            this.InitializeComponent();
            ViewModel = new OobePowerToysModule(OobeShellPage.OobeShellHandler.Modules[(int)PowerToysModules.AdvancedPaste]);
            DataContext = ViewModel;
        }

        private void SettingsLaunchButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (OobeShellPage.OpenMainWindowCallback != null)
            {
                OobeShellPage.OpenMainWindowCallback(typeof(AdvancedPastePage));
            }

            ViewModel.LogOpeningSettingsEvent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.LogOpeningModuleEvent();
            AdvancedPasteUIHotkeyControl.Keys = SettingsRepository<AdvancedPasteSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.AdvancedPasteUIShortcut.GetKeysList();
            PasteAsPlainTextHotkeyControl.Keys = SettingsRepository<AdvancedPasteSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.PasteAsPlainTextShortcut.GetKeysList();
            PasteAsMarkdownHotkeyControl.Keys = SettingsRepository<AdvancedPasteSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.PasteAsMarkdownShortcut.GetKeysList();

            // TODO(stefan): Check how to remove additional space if item is set to Collapsed.
            if (PasteAsMarkdownHotkeyControl.Keys.Count > 0)
            {
                PasteAsMarkdownHotkeyControl.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            }

            PasteAsJsonHotkeyControl.Keys = SettingsRepository<AdvancedPasteSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.PasteAsJsonShortcut.GetKeysList();
            if (PasteAsJsonHotkeyControl.Keys.Count > 0)
            {
                PasteAsJsonHotkeyControl.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.LogClosingModuleEvent();
        }
    }
}
