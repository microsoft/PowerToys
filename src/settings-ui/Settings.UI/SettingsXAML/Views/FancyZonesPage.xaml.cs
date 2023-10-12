// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class FancyZonesPage : Page, IRefreshablePage
    {
        private FancyZonesViewModel ViewModel { get; set; }

        public FancyZonesPage()
        {
            InitializeComponent();
            var settingsUtils = new SettingsUtils();
            ViewModel = new FancyZonesViewModel(settingsUtils, SettingsRepository<GeneralSettings>.GetInstance(settingsUtils), SettingsRepository<FancyZonesSettings>.GetInstance(settingsUtils), ShellPage.SendDefaultIPCMessage);
            DataContext = ViewModel;

            List<string> deletedModules = UninstallModuleUtilities.ReadWordsFromFile("uninstalled_modules");
            if (UninstallModuleUtilities.DoesListContainWord(deletedModules, "FancyZones"))
            {
                this.IfUninstalledModule.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                this.NoModuleSection.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            }
        }

        private void OpenColorsSettings_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Helpers.StartProcessHelper.Start(Helpers.StartProcessHelper.ColorsSettings);
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }
    }
}
