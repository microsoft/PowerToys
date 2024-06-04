// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Security.Credentials;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class AdvancedPastePage : Page, IRefreshablePage
    {
        private AdvancedPasteViewModel ViewModel { get; set; }

        public ICommand SaveOpenAIKeyCommand => new RelayCommand(SaveOpenAIKey);

        public AdvancedPastePage()
        {
            var settingsUtils = new SettingsUtils();
            ViewModel = new AdvancedPasteViewModel(
                settingsUtils,
                SettingsRepository<GeneralSettings>.GetInstance(settingsUtils),
                SettingsRepository<AdvancedPasteSettings>.GetInstance(settingsUtils),
                ShellPage.SendDefaultIPCMessage);
            DataContext = ViewModel;
            InitializeComponent();
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }

        private void SaveOpenAIKey()
        {
            if (!string.IsNullOrEmpty(AdvancedPaste_EnableAIDialogOpenAIApiKey.Text))
            {
                ViewModel.EnableAI(AdvancedPaste_EnableAIDialogOpenAIApiKey.Text);
            }
        }

        private async void AdvancedPaste_EnableAIButton_Click(object sender, RoutedEventArgs e)
        {
            var resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;
            EnableAIDialog.PrimaryButtonText = resourceLoader.GetString("EnableAIDialog_SaveBtnText");
            EnableAIDialog.SecondaryButtonText = resourceLoader.GetString("EnableAIDialog_CancelBtnText");
            EnableAIDialog.PrimaryButtonCommand = SaveOpenAIKeyCommand;

            AdvancedPaste_EnableAIDialogOpenAIApiKey.Text = string.Empty;

            await ShowEnableDialogAsync();
        }

        private async Task ShowEnableDialogAsync()
        {
            await EnableAIDialog.ShowAsync();
        }

        private void AdvancedPaste_DisableAIButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.DisableAI();
        }

        private void AdvancedPaste_EnableAIDialogOpenAIApiKey_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (AdvancedPaste_EnableAIDialogOpenAIApiKey.Text.Length > 0)
            {
                EnableAIDialog.IsPrimaryButtonEnabled = true;
            }
            else
            {
                EnableAIDialog.IsPrimaryButtonEnabled = false;
            }
        }
    }
}
