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
    public sealed partial class PeekPage : Page, IRefreshablePage
    {
        private PeekViewModel ViewModel { get; set; }

        public PeekPage()
        {
            var settingsUtils = new SettingsUtils();
            ViewModel = new PeekViewModel(settingsUtils, SettingsRepository<GeneralSettings>.GetInstance(settingsUtils), ShellPage.SendDefaultIPCMessage);
            DataContext = ViewModel;
            InitializeComponent();

            List<string> deletedModules = UninstallModuleUtilities.ReadWordsFromFile("uninstalled_modules");
            if (UninstallModuleUtilities.DoesListContainWord(deletedModules, "Peek"))
            {
                this.IfUninstalledModule.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                this.NoModuleSection.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            }
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }
    }
}
