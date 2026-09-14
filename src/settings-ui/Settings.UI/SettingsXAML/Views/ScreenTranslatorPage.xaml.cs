// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class ScreenTranslatorPage : NavigablePage, IRefreshablePage
    {
        private ScreenTranslatorViewModel ViewModel { get; set; }

        public ScreenTranslatorPage()
        {
            var settingsUtils = SettingsUtils.Default;
            ViewModel = new ScreenTranslatorViewModel(
                settingsUtils,
                SettingsRepository<GeneralSettings>.GetInstance(settingsUtils),
                SettingsRepository<ScreenTranslatorSettings>.GetInstance(settingsUtils),
                ShellPage.SendDefaultIPCMessage);
            DataContext = ViewModel;
            InitializeComponent();
            Loaded += (s, e) =>
            {
                ViewModel.OnPageLoaded();
                ClearApiKeyInputs();
            };
        }

        private void ClearApiKeyInputs()
        {
            if (AzureApiKeyPasswordBox != null)
            {
                AzureApiKeyPasswordBox.Password = string.Empty;
            }

            if (LibreTranslateApiKeyPasswordBox != null)
            {
                LibreTranslateApiKeyPasswordBox.Password = string.Empty;
            }
        }

        private void SaveAzureApiKey_Click(object sender, RoutedEventArgs e)
        {
            if (AzureApiKeyPasswordBox != null)
            {
                ViewModel.SaveAzureApiKey(AzureApiKeyPasswordBox.Password);
                AzureApiKeyPasswordBox.Password = string.Empty;
            }
        }

        private void ClearAzureApiKey_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.RemoveAzureApiKey();
            if (AzureApiKeyPasswordBox != null)
            {
                AzureApiKeyPasswordBox.Password = string.Empty;
            }
        }

        private void SaveLibreTranslateApiKey_Click(object sender, RoutedEventArgs e)
        {
            if (LibreTranslateApiKeyPasswordBox != null)
            {
                ViewModel.SaveLibreTranslateApiKey(LibreTranslateApiKeyPasswordBox.Password);
                LibreTranslateApiKeyPasswordBox.Password = string.Empty;
            }
        }

        private void ClearLibreTranslateApiKey_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.RemoveLibreTranslateApiKey();
            if (LibreTranslateApiKeyPasswordBox != null)
            {
                LibreTranslateApiKeyPasswordBox.Password = string.Empty;
            }
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
            ClearApiKeyInputs();
        }

        public static Visibility BoolToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
    }
}
