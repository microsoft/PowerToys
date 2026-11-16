// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class RobocopyUIPage : NavigablePage, IRefreshablePage
    {
        private RobocopyUIViewModel ViewModel { get; set; }

        public RobocopyUIPage()
        {
            var settingsUtils = SettingsUtils.Default;
            ViewModel = new RobocopyUIViewModel(
                SettingsRepository<GeneralSettings>.GetInstance(settingsUtils),
                SettingsRepository<RobocopyUISettings>.GetInstance(settingsUtils),
                ShellPage.SendDefaultIPCMessage);
            DataContext = ViewModel;
            InitializeComponent();
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }

        private void HyperlinkButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            // Navigate to Advanced Paste settings page
            ShellPage.OpenMainWindowCallback(typeof(AdvancedPastePage));
        }
    }
}
