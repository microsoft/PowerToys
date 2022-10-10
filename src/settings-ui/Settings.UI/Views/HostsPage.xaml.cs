// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Xaml.Controls;
using Settings.UI.Library.ViewModels;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class HostsPage : Page
    {
        private HostsViewModel ViewModel { get; }

        public HostsPage()
        {
            InitializeComponent();
            var settingsUtils = new SettingsUtils();
            ViewModel = new HostsViewModel(settingsUtils, SettingsRepository<GeneralSettings>.GetInstance(settingsUtils), SettingsRepository<HostsSettings>.GetInstance(settingsUtils), ShellPage.SendDefaultIPCMessage, App.IsElevated);
        }
    }
}
