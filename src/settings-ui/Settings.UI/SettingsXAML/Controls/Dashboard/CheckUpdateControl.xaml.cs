// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Services;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class CheckUpdateControl : UserControl
    {
        public bool UpdateAvailable { get; set; }

        public UpdatingSettings UpdateSettingsConfig { get; set; }

        public CheckUpdateControl()
        {
            InitializeComponent();
            UpdateSettingsConfig = UpdatingSettings.LoadSettings();
            UpdateAvailable = UpdateSettingsConfig != null && (UpdateSettingsConfig.State == UpdatingSettings.UpdatingState.ReadyToInstall || UpdateSettingsConfig.State == UpdatingSettings.UpdatingState.ReadyToDownload);
        }

        private void SWVersionButtonClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            NavigationService.Navigate(typeof(GeneralPage));
        }
    }
}
