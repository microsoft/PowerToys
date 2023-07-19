// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class HostsPage : Page, IRefreshablePage
    {
        private HostsViewModel ViewModel { get; }

        public HostsPage()
        {
            InitializeComponent();
            var settingsUtils = new SettingsUtils();
            ViewModel = new HostsViewModel(settingsUtils, SettingsRepository<GeneralSettings>.GetInstance(settingsUtils), SettingsRepository<HostsSettings>.GetInstance(settingsUtils), ShellPage.SendDefaultIPCMessage, App.IsElevated);
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }
    }
}
