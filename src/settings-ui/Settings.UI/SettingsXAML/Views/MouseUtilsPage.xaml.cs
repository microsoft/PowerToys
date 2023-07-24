// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class MouseUtilsPage : Page, IRefreshablePage
    {
        private MouseUtilsViewModel ViewModel { get; set; }

        public MouseUtilsPage()
        {
            try
            {
                // By mistake, the first release of Find My Mouse was saving settings in two places at the same time.
                // Delete the wrong path for Find My Mouse settings.
                var tempSettingsUtils = new SettingsUtils();
                if (tempSettingsUtils.SettingsExists("Find My Mouse"))
                {
                    var settingsFilePath = tempSettingsUtils.GetSettingsFilePath("Find My Mouse");
                    System.IO.File.Delete(settingsFilePath);
                    tempSettingsUtils.DeleteSettings("Find My Mouse");
                }
            }
            catch (System.Exception)
            {
            }

            var settingsUtils = new SettingsUtils();
            ViewModel = new MouseUtilsViewModel(
                settingsUtils,
                SettingsRepository<GeneralSettings>.GetInstance(settingsUtils),
                SettingsRepository<FindMyMouseSettings>.GetInstance(settingsUtils),
                SettingsRepository<MouseHighlighterSettings>.GetInstance(settingsUtils),
                SettingsRepository<MouseJumpSettings>.GetInstance(settingsUtils),
                SettingsRepository<MousePointerCrosshairsSettings>.GetInstance(settingsUtils),
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
