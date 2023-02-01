// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class PowerOcrPage : Page, IRefreshablePage
    {
        private PowerOcrViewModel ViewModel { get; set; }

        public PowerOcrPage()
        {
            var settingsUtils = new SettingsUtils();
            ViewModel = new PowerOcrViewModel(
                settingsUtils,
                SettingsRepository<GeneralSettings>.GetInstance(settingsUtils),
                SettingsRepository<PowerOcrSettings>.GetInstance(settingsUtils),
                ShellPage.SendDefaultIPCMessage);
            DataContext = ViewModel;
            InitializeComponent();
        }

        private void TextExtractor_ComboBox_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            /**
          * UWP hack
          * because UWP load the bound ItemSource of the ComboBox asynchronous,
          * so after InitializeComponent() the ItemSource is still empty and can't automatically select a entry.
          * Selection via SelectedItem and SelectedValue is still not working too
          */
            ViewModel.UpdateLanguages();
        }

        private void TextExtractor_ComboBox_DropDownOpened(object sender, object e)
        {
            ViewModel.UpdateLanguages();
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }
    }
}
