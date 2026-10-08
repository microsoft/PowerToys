// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }

        private List<CharacterSetPickerGroup> _characterSetGroups = [];
        private List<CharacterSetPickerGroup> _visibleCharacterSetGroups = [];
        private bool _suppressSelectionSync;

        private async void EditCharacterSetsButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = new HashSet<PowerAccentLanguageModel>(ViewModel.SelectedLanguageOptions);
            _characterSetGroups = ViewModel.LanguageGroups
                .Select(group => new CharacterSetPickerGroup(group.Group, group.Select(l => new CharacterSetPickerItem(l) { IsChecked = selected.Contains(l) })))
                .ToList();

            CharacterSetsSearchBox.Text = string.Empty;
            ApplyCharacterSetsFilter(string.Empty);

            CharacterSetsDialog.XamlRoot = XamlRoot;
            await CharacterSetsDialog.ShowAsync();
        }

        private void ApplyCharacterSetsFilter(string query)
        {
            query = query?.Trim() ?? string.Empty;

            _visibleCharacterSetGroups = string.IsNullOrEmpty(query)
                ? _characterSetGroups
                : _characterSetGroups
                    .Select(group => new CharacterSetPickerGroup(
                        group.Header,
                        group.Where(item => item.Language.Language.Contains(query, StringComparison.CurrentCultureIgnoreCase))))
                    .Where(group => group.Count > 0)
                    .ToList();

            AvailableCharacterSetsViewSource.Source = _visibleCharacterSetGroups;

            // Rebuilding ItemsSource clears the ListView selection; don't let that uncheck items.
            _suppressSelectionSync = true;
            AvailableCharacterSetsList.ItemsSource = AvailableCharacterSetsViewSource.View;
            _suppressSelectionSync = false;
            SyncListSelection();

            NoCharacterSetsFoundText.Visibility = _visibleCharacterSetGroups.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateCharacterSetsDialogState();
        }

        private void CharacterSetsSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                ApplyCharacterSetsFilter(sender.Text);
            }
        }

        /// <summary>
        /// Mirrors <see cref="CharacterSetPickerItem.IsChecked"/> (the source of truth, which survives filtering)
        /// onto the ListView's selection.
        /// </summary>
        private void SyncListSelection()
        {
            _suppressSelectionSync = true;
            try
            {
                AvailableCharacterSetsList.SelectedItems.Clear();
                foreach (var item in _visibleCharacterSetGroups.SelectMany(group => group).Where(item => item.IsChecked))
                {
                    AvailableCharacterSetsList.SelectedItems.Add(item);
                }
            }
            finally
            {
                _suppressSelectionSync = false;
            }
        }

        private void AvailableCharacterSetsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelectionSync)
            {
                return;
            }

            foreach (var item in e.AddedItems.OfType<CharacterSetPickerItem>())
            {
                item.IsChecked = true;
            }

            foreach (var item in e.RemovedItems.OfType<CharacterSetPickerItem>())
            {
                item.IsChecked = false;
            }

            UpdateCharacterSetsDialogState();
        }

        private void SelectAllCharacterSetsCheckBox_Click(object sender, RoutedEventArgs e)
        {
            // Applies to the visible (filtered) sets. From the mixed state, a click selects all.
            var visibleItems = _visibleCharacterSetGroups.SelectMany(group => group).ToList();
            bool check = !visibleItems.All(item => item.IsChecked);
            foreach (var item in visibleItems)
            {
                item.IsChecked = check;
            }

            SyncListSelection();
            UpdateCharacterSetsDialogState();
        }

        private List<PowerAccentLanguageModel> GetCheckedCharacterSets() =>
            _characterSetGroups.SelectMany(group => group).Where(item => item.IsChecked).Select(item => item.Language).ToList();

        private void UpdateCharacterSetsDialogState()
        {
            var visibleItems = _visibleCharacterSetGroups.SelectMany(group => group).ToList();
            int visibleChecked = visibleItems.Count(item => item.IsChecked);
            SelectAllCharacterSetsCheckBox.IsEnabled = visibleItems.Count > 0;
            SelectAllCharacterSetsCheckBox.IsChecked = visibleChecked == 0 ? false : visibleChecked == visibleItems.Count ? true : null;

            int totalChecked = GetCheckedCharacterSets().Count;
            int total = _characterSetGroups.Sum(group => group.Count);
            SelectedCharacterSetsCountText.Text = string.Format(
                CultureInfo.CurrentCulture,
                ResourceLoaderInstance.ResourceLoader.GetString("QuickAccent_CharacterSetsDialog_SelectedCount"),
                totalChecked,
                total);

            // Require at least one set; with none selected Quick Accent would never show anything.
            CharacterSetsDialog.IsPrimaryButtonEnabled = totalChecked > 0;
        }

        private void CharacterSetsDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            ViewModel.SelectedLanguageOptions = GetCheckedCharacterSets().ToArray();
        }

        private void ReferenceGuideButton_Click(object sender, RoutedEventArgs e)
        {
            // Pass the currently selected language codes so the reference guide can
            // surface them at the top and mark them as selected.
            var selectedCodes = ViewModel.SelectedLanguageOptions
                .Select(l => l.LanguageCode)
                .ToArray();

            ((App)Application.Current).OpenPowerAccentReferenceGuideWindow(selectedCodes);
        }
    }
}
