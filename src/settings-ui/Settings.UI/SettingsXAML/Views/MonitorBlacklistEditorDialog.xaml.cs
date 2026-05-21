// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.UI.Xaml.Controls;
using PowerDisplay.Models;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    /// <summary>
    /// Dialog for creating or editing one user-customized monitor blacklist entry.
    /// EdidId input is forced upper-case at typing time and validated against
    /// <c>[A-Za-z0-9]{1,16}</c>; duplicates against the built-in or existing custom
    /// list show informational warnings (the user is still allowed to add them per
    /// design — the displayed list de-duplicates).
    /// </summary>
    public sealed partial class MonitorBlacklistEditorDialog : ContentDialog
    {
        private static readonly Regex EdidIdRegex = new("^[A-Za-z0-9]{1,16}$", RegexOptions.Compiled);

        private readonly HashSet<string> _builtInIds;
        private readonly HashSet<string> _existingCustomIds;
        private readonly string? _originalEdidId;

        /// <summary>
        /// Gets the entry produced by the dialog after Save. Null if the dialog was cancelled.
        /// </summary>
        public MonitorBlacklistEntry? ResultEntry { get; private set; }

        public MonitorBlacklistEditorDialog(
            IEnumerable<MonitorBlacklistEntry> builtIn,
            IEnumerable<MonitorBlacklistEntry> existingCustom,
            MonitorBlacklistEntry? existing = null)
        {
            this.InitializeComponent();

            _builtInIds = new HashSet<string>(
                builtIn.Select(e => e.EdidId.ToUpperInvariant()),
                System.StringComparer.OrdinalIgnoreCase);

            _existingCustomIds = new HashSet<string>(
                existingCustom.Select(e => e.EdidId.ToUpperInvariant()),
                System.StringComparer.OrdinalIgnoreCase);

            _originalEdidId = existing?.EdidId;

            // Editing an existing entry: pre-fill, and remove its own EdidId from the
            // duplicate-of-custom set so saving without changing the EdidId is allowed.
            if (existing != null)
            {
                EdidIdTextBox.Text = existing.EdidId;
                CommentsTextBox.Text = existing.Comments ?? string.Empty;
                _existingCustomIds.Remove(existing.EdidId.ToUpperInvariant());
            }

            var resourceLoader = ResourceLoaderInstance.ResourceLoader;
            Title = resourceLoader.GetString("PowerDisplay_MonitorBlacklistEditor_Title");
            PrimaryButtonText = resourceLoader.GetString("PowerDisplay_MonitorBlacklistEditor_PrimaryButton");
            CloseButtonText = resourceLoader.GetString("PowerDisplay_MonitorBlacklistEditor_CloseButton");

            this.PrimaryButtonClick += OnPrimaryButtonClick;
            UpdateValidationState();
        }

        private void EdidIdTextBox_TextChanged(object sender, TextChangedEventArgs e)
            => UpdateValidationState();

        private void UpdateValidationState()
        {
            var input = (EdidIdTextBox.Text ?? string.Empty).Trim().ToUpperInvariant();
            var resourceLoader = ResourceLoaderInstance.ResourceLoader;

            if (!EdidIdRegex.IsMatch(input))
            {
                ValidationInfoBar.Message = resourceLoader.GetString(
                    "PowerDisplay_MonitorBlacklistEditor_Validation_InvalidEdid");
                ValidationInfoBar.Severity = InfoBarSeverity.Warning;
                ValidationInfoBar.IsOpen = true;
                IsPrimaryButtonEnabled = false;
                return;
            }

            if (_builtInIds.Contains(input))
            {
                ValidationInfoBar.Message = resourceLoader.GetString(
                    "PowerDisplay_MonitorBlacklistEditor_Validation_DuplicateOfBuiltIn");
                ValidationInfoBar.Severity = InfoBarSeverity.Informational;
                ValidationInfoBar.IsOpen = true;
                IsPrimaryButtonEnabled = true; // allowed; UI dedups
                return;
            }

            if (_existingCustomIds.Contains(input))
            {
                ValidationInfoBar.Message = resourceLoader.GetString(
                    "PowerDisplay_MonitorBlacklistEditor_Validation_DuplicateOfCustom");
                ValidationInfoBar.Severity = InfoBarSeverity.Warning;
                ValidationInfoBar.IsOpen = true;
                IsPrimaryButtonEnabled = false;
                return;
            }

            ValidationInfoBar.IsOpen = false;
            IsPrimaryButtonEnabled = true;
        }

        private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var edid = (EdidIdTextBox.Text ?? string.Empty).Trim().ToUpperInvariant();
            if (!EdidIdRegex.IsMatch(edid))
            {
                args.Cancel = true;
                return;
            }

            ResultEntry = new MonitorBlacklistEntry
            {
                EdidId = edid,
                Comments = (CommentsTextBox.Text ?? string.Empty).Trim(),
            };
        }
    }
}
