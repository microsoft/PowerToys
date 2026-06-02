// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using KeyboardManagerEditorUI.Helpers;
using KeyboardManagerEditorUI.Templates;
using KeyboardManagerEditorUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace KeyboardManagerEditorUI.Controls
{
    public sealed partial class CommandTemplatePickerControl : UserControl
    {
        public CommandTemplatePickerControl()
        {
            InitializeComponent();
            ViewModel = new CommandTemplatePickerViewModel();
        }

        public CommandTemplatePickerViewModel ViewModel { get; }

        public event EventHandler? SelectionChanged;

        public event EventHandler? MissingTemplateKeepRequested;

        public TemplateResolver.Resolved? ResolveCurrent()
        {
            if (ViewModel.SelectedTemplate is null)
            {
                return null;
            }

            return TemplateResolver.Resolve(
                ViewModel.SelectedTemplate,
                ViewModel.CollectParameterValues());
        }

        public string? CurrentTemplateId => ViewModel.SelectedTemplate?.Id;

        public Dictionary<string, string> CurrentParameterValues => ViewModel.CollectParameterValues();

        public void LoadExisting(string templateId, IReadOnlyDictionary<string, string>? values)
        {
            try
            {
                ViewModel.LoadExisting(templateId, values);
                MissingTemplateInfoBar.IsOpen = false;
            }
            catch (InvalidOperationException)
            {
                ShowMissingTemplateInfoBar();
            }
        }

        public void Reset()
        {
            ViewModel.Clear();
            MissingTemplateInfoBar.IsOpen = false;
        }

        /// <summary>
        /// Selects a command template by id (driven by the host action menu) and notifies listeners.
        /// </summary>
        public void SelectCommand(string templateId)
        {
            ViewModel.SelectTemplate(templateId);
            MissingTemplateInfoBar.IsOpen = false;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Gets the display name of the currently selected command (empty when none selected).
        /// </summary>
        public string CurrentCommandDisplay =>
            ViewModel.SelectedTemplate is { } t
                ? ResourceHelper.GetString(t.DisplayResourceKey)
                : string.Empty;

        private void ShowMissingTemplateInfoBar()
        {
            MissingTemplateInfoBar.IsOpen = true;
        }

        private void MissingTemplateKeepButton_Click(object sender, RoutedEventArgs e)
        {
            MissingTemplateInfoBar.IsOpen = false;
            MissingTemplateKeepRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
