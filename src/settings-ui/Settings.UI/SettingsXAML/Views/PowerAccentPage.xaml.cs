// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System.Linq;
using CommunityToolkit.WinUI;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class PowerAccentPage : NavigablePage, IRefreshablePage
    {
        private PowerAccentViewModel ViewModel { get; set; }

        public PowerAccentPage()
        {
            var settingsUtils = SettingsUtils.Default;
            ViewModel = new PowerAccentViewModel(settingsUtils, SettingsRepository<GeneralSettings>.GetInstance(settingsUtils), ShellPage.SendDefaultIPCMessage);
            DataContext = ViewModel;
            this.InitializeComponent();
            this.InitializeControlsStates();
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }

        private void InitializeControlsStates()
        {
            SetCheckBoxStatus();
        }

        private void SetCheckBoxStatus()
        {
            if (ViewModel.SelectedLanguageOptions.Length == 0)
            {
                this.QuickAccent_SelectedLanguage_All.IsChecked = false;
                this.QuickAccent_SelectedLanguage_All.IsThreeState = false;
            }
            else if (ViewModel.AllSelected)
            {
                this.QuickAccent_SelectedLanguage_All.IsChecked = true;
                this.QuickAccent_SelectedLanguage_All.IsThreeState = false;
            }
            else
            {
                this.QuickAccent_SelectedLanguage_All.IsThreeState = true;
                this.QuickAccent_SelectedLanguage_All.IsChecked = null;
            }
        }

        private void QuickAccent_SelectedLanguage_SelectAll(object sender, RoutedEventArgs e)
        {
            this.QuickAccent_Language_Select.SelectAllSafe();
        }

        private void QuickAccent_SelectedLanguage_UnselectAll(object sender, RoutedEventArgs e)
        {
            this.QuickAccent_Language_Select.DeselectAll();
        }

        private bool loadingLanguageListDontTriggerSelectionChanged;

        private void QuickAccent_SelectedLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (loadingLanguageListDontTriggerSelectionChanged)
            {
                return;
            }

            var listView = sender as ListView;

            ViewModel.SelectedLanguageOptions = listView.SelectedItems
                .Select(item => item as PowerAccentLanguageModel)
                .ToArray();

            SetCheckBoxStatus();
        }

        private void QuickAccent_Language_Select_Loaded(object sender, RoutedEventArgs e)
        {
            loadingLanguageListDontTriggerSelectionChanged = true;
            foreach (var languageOption in ViewModel.SelectedLanguageOptions)
            {
                this.QuickAccent_Language_Select.SelectedItems.Add(languageOption);
            }

            loadingLanguageListDontTriggerSelectionChanged = false;
        }
    }
}
