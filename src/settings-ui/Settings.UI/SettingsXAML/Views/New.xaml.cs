// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using System.Windows;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class NewPage : Page, IRefreshablePage
    {
        private NewViewModel ViewModel { get; set; }

        public NewPage()
        {
            InitializeComponent();
            var settings_utils = new SettingsUtils();
            ViewModel = new NewViewModel(settings_utils, SettingsRepository<GeneralSettings>.GetInstance(settings_utils), ShellPage.SendDefaultIPCMessage);
            DataContext = ViewModel;
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }
    }
}
