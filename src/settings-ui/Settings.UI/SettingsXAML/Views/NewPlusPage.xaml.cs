// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class NewPlusPage : Page, IRefreshablePage
    {
        private NewPlusViewModel ViewModel { get; set; }

        public NewPlusPage()
        {
            InitializeComponent();
            var settings_utils = new SettingsUtils();
            ViewModel = new NewPlusViewModel(settings_utils, SettingsRepository<GeneralSettings>.GetInstance(settings_utils), ShellPage.SendDefaultIPCMessage);
            DataContext = ViewModel;
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }
    }
}
