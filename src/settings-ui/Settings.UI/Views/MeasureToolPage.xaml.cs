// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class MeasureToolPage : Page, IRefreshablePage
    {
        private MeasureToolViewModel ViewModel { get; set; }

        private const string ModuleName = "Measure Tool";

        public MeasureToolPage()
        {
            var settingsUtils = new SettingsUtils();
            ViewModel = new MeasureToolViewModel(
                settingsUtils,
                SettingsRepository<GeneralSettings>.GetInstance(settingsUtils),
                SettingsRepository<MeasureToolSettings>.GetInstance(settingsUtils),
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
