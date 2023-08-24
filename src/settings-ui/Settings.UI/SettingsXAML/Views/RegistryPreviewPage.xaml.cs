// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class RegistryPreviewPage : Page, IRefreshablePage
    {
        private RegistryPreviewViewModel ViewModel { get; set; }

        public RegistryPreviewPage()
        {
            var settingsUtils = new SettingsUtils();
            ViewModel = new RegistryPreviewViewModel(
                SettingsRepository<GeneralSettings>.GetInstance(settingsUtils),
                SettingsRepository<RegistryPreviewSettings>.GetInstance(settingsUtils),
                ShellPage.SendDefaultIPCMessage);
            DataContext = ViewModel;
            InitializeComponent();
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }
    }
}
