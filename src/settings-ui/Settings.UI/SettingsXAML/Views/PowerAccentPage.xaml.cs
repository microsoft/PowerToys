// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Services;
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
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }

        private List<CharacterSetPickerGroup> _availableCharacterSetGroups = [];

        private async void AddCharacterSetsButton_Click(object sender, RoutedEventArgs e)
        {
            _availableCharacterSetGroups = ViewModel.GetAvailableLanguageGroups()
                .Select(group => new CharacterSetPickerGroup(group.Group, group.Select(l => new CharacterSetPickerItem(l))))
                .ToList();

            CharacterSetsSearchBox.Text = string.Empty;
            ApplyCharacterSetsFilter(string.Empty);
            UpdateAddButtonState();

            AddCharacterSetsDialog.XamlRoot = XamlRoot;
            await AddCharacterSetsDialog.ShowAsync();
        }

        private void ApplyCharacterSetsFilter(string query)
        {
            query = query?.Trim() ?? string.Empty;

            var filtered = string.IsNullOrEmpty(query)
                ? _availableCharacterSetGroups
                : _availableCharacterSetGroups
                    .Select(group => new CharacterSetPickerGroup(
                        group.Header,
                        group.Where(item => item.Language.Language.Contains(query, StringComparison.CurrentCultureIgnoreCase))))
                    .Where(group => group.Count > 0)
                    .ToList();

            AvailableCharacterSetsViewSource.Source = filtered;
            AvailableCharacterSetsList.ItemsSource = AvailableCharacterSetsViewSource.View;
            NoCharacterSetsFoundText.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void CharacterSetsSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                ApplyCharacterSetsFilter(sender.Text);
            }
        }

        private void AvailableCharacterSetsList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is CharacterSetPickerItem item)
            {
                item.IsChecked = !item.IsChecked;
                UpdateAddButtonState();
            }
        }

        private void CharacterSetCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            // Checked/Unchecked fire before the TwoWay binding writes back, so sync the item explicitly.
            if (sender is CheckBox { DataContext: CharacterSetPickerItem item } checkBox)
            {
                item.IsChecked = checkBox.IsChecked == true;
            }

            UpdateAddButtonState();
        }

        private IEnumerable<PowerAccentLanguageModel> GetCheckedCharacterSets() =>
            _availableCharacterSetGroups.SelectMany(group => group).Where(item => item.IsChecked).Select(item => item.Language);

        private void UpdateAddButtonState()
        {
            AddCharacterSetsDialog.IsPrimaryButtonEnabled = GetCheckedCharacterSets().Any();
        }

        private void AddCharacterSetsDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            ViewModel.AddLanguages(GetCheckedCharacterSets().ToList());
        }

        private void RemoveCharacterSet_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is PowerAccentLanguageModel language)
            {
                ViewModel.RemoveLanguage(language);
            }
        }

        private void ReferenceGuideButton_Click(object sender, RoutedEventArgs e)
        {
            // Pass the currently selected language codes so the reference guide can
            // surface them at the top and mark them as selected.
            var selectedCodes = ViewModel.SelectedLanguageOptions
                .Select(l => l.LanguageCode)
                .ToArray();

            NavigationService.Navigate<PowerAccentReferenceGuidePage>(selectedCodes);
        }
    }
}
