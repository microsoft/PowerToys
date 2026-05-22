// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PowerDisplay.Models;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    /// <summary>
    /// Dialog that fully manages the monitor blacklist — both the read-only built-in list
    /// and the user-editable custom list. Uses a two-mode internal layout (list mode and
    /// form mode) toggled by Grid visibility to avoid WinUI's one-ContentDialog-at-a-time
    /// limitation when adding or editing entries.
    /// </summary>
    public sealed partial class MonitorBlacklistDialog : ContentDialog
    {
        private static readonly Regex EdidIdRegex = new("^[A-Za-z0-9]{1,16}$", RegexOptions.Compiled);

        private MonitorBlacklistEntry? _editingEntry;

        public PowerDisplayViewModel ViewModel { get; }

        public MonitorBlacklistDialog(PowerDisplayViewModel viewModel)
        {
            ViewModel = viewModel;
            this.InitializeComponent();

            var resourceLoader = ResourceLoaderInstance.ResourceLoader;
            Title = resourceLoader.GetString("PowerDisplay_MonitorBlacklist_DialogTitle");
            CloseButtonText = resourceLoader.GetString("PowerDisplay_MonitorBlacklist_CloseButton");

            UpdateCustomEmptyHintVisibility();
            ViewModel.DisplayedCustomBlacklist.CollectionChanged += (s, e) => UpdateCustomEmptyHintVisibility();
        }

        private void UpdateCustomEmptyHintVisibility()
        {
            CustomEmptyHint.Visibility = ViewModel.DisplayedCustomBlacklist.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void EnterFormMode(MonitorBlacklistEntry? existing)
        {
            _editingEntry = existing;
            var resourceLoader = ResourceLoaderInstance.ResourceLoader;
            FormTitleText.Text = resourceLoader.GetString(
                existing == null
                    ? "PowerDisplay_MonitorBlacklist_Form_AddTitle"
                    : "PowerDisplay_MonitorBlacklist_Form_EditTitle");

            EdidIdTextBox.Text = existing?.EdidId ?? string.Empty;
            CommentsTextBox.Text = existing?.Comments ?? string.Empty;

            ListModeRoot.Visibility = Visibility.Collapsed;
            FormModeRoot.Visibility = Visibility.Visible;
            UpdateValidationState();

            EdidIdTextBox.Focus(FocusState.Programmatic);
        }

        private void EnterListMode()
        {
            _editingEntry = null;
            FormModeRoot.Visibility = Visibility.Collapsed;
            ListModeRoot.Visibility = Visibility.Visible;
            ValidationInfoBar.IsOpen = false;
        }

        private void AddEntry_Click(object sender, RoutedEventArgs e) => EnterFormMode(null);

        private void EditEntry_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is MonitorBlacklistEntry entry)
            {
                EnterFormMode(entry);
            }
        }

        private void DeleteEntry_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is MonitorBlacklistEntry entry)
            {
                ViewModel.DeleteCustomBlacklistEntry(entry);
            }
        }

        private void EdidIdTextBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateValidationState();

        private void UpdateValidationState()
        {
            var input = (EdidIdTextBox.Text ?? string.Empty).Trim().ToUpperInvariant();
            var resourceLoader = ResourceLoaderInstance.ResourceLoader;

            if (!EdidIdRegex.IsMatch(input))
            {
                ValidationInfoBar.Message = resourceLoader.GetString("PowerDisplay_MonitorBlacklistEditor_Validation_InvalidEdid");
                ValidationInfoBar.Severity = InfoBarSeverity.Warning;
                ValidationInfoBar.IsOpen = true;
                FormSaveButton.IsEnabled = false;
                return;
            }

            var builtInIds = new HashSet<string>(
                ViewModel.BuiltInBlacklist.Select(e => e.EdidId.ToUpperInvariant()),
                System.StringComparer.OrdinalIgnoreCase);
            if (builtInIds.Contains(input))
            {
                ValidationInfoBar.Message = resourceLoader.GetString("PowerDisplay_MonitorBlacklistEditor_Validation_DuplicateOfBuiltIn");
                ValidationInfoBar.Severity = InfoBarSeverity.Informational;
                ValidationInfoBar.IsOpen = true;
                FormSaveButton.IsEnabled = true;
                return;
            }

            var existingCustomIds = new HashSet<string>(
                ViewModel.CustomBlacklist
                    .Where(e => !ReferenceEquals(e, _editingEntry))
                    .Select(e => e.EdidId.ToUpperInvariant()),
                System.StringComparer.OrdinalIgnoreCase);
            if (existingCustomIds.Contains(input))
            {
                ValidationInfoBar.Message = resourceLoader.GetString("PowerDisplay_MonitorBlacklistEditor_Validation_DuplicateOfCustom");
                ValidationInfoBar.Severity = InfoBarSeverity.Warning;
                ValidationInfoBar.IsOpen = true;
                FormSaveButton.IsEnabled = false;
                return;
            }

            ValidationInfoBar.IsOpen = false;
            FormSaveButton.IsEnabled = true;
        }

        private void FormCancel_Click(object sender, RoutedEventArgs e) => EnterListMode();

        private void FormSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var edid = (EdidIdTextBox.Text ?? string.Empty).Trim().ToUpperInvariant();
                if (!EdidIdRegex.IsMatch(edid))
                {
                    return;
                }

                var newEntry = new MonitorBlacklistEntry
                {
                    EdidId = edid,
                    Comments = (CommentsTextBox.Text ?? string.Empty).Trim(),
                };

                if (_editingEntry != null)
                {
                    ViewModel.UpdateCustomBlacklistEntry(_editingEntry, newEntry);
                }
                else
                {
                    ViewModel.AddCustomBlacklistEntry(newEntry);
                }

                EnterListMode();
            }
            catch (Exception ex)
            {
                // Capture details before WinUI wraps the failure into a generic COMException.
                Logger.LogError("MonitorBlacklistDialog.FormSave_Click failed", ex);
                throw;
            }
        }
    }
}
