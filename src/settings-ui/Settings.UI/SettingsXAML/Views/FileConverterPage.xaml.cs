// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class FileConverterPage : NavigablePage, IRefreshablePage
    {
        private FileConverterViewModel ViewModel { get; }

        public FileConverterPage()
        {
            var settingsUtils = SettingsUtils.Default;
            ViewModel = new FileConverterViewModel(settingsUtils, SettingsRepository<GeneralSettings>.GetInstance(settingsUtils), ShellPage.SendDefaultIPCMessage);
            DataContext = ViewModel;
            InitializeComponent();
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }
    }
}
