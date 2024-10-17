// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class PowerPreviewPage : Page, IRefreshablePage
    {
        public PowerPreviewViewModel ViewModel { get; set; }

        public PowerPreviewPage()
        {
            InitializeComponent();
            var settingsUtils = new SettingsUtils();
            ViewModel = new PowerPreviewViewModel(SettingsRepository<PowerPreviewSettings>.GetInstance(settingsUtils), SettingsRepository<GeneralSettings>.GetInstance(settingsUtils), ShellPage.SendDefaultIPCMessage);
            DataContext = ViewModel;
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }
    }
}
