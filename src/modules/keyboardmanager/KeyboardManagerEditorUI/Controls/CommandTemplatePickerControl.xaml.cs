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
using Microsoft.UI.Xaml.Media;

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

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            BuildFlyout(TemplateMenuFlyout, CommandTemplateCatalog.Instance.Data);
        }

        private void BuildFlyout(MenuFlyout flyout, PowerToysCliCatalog catalog)
        {
            flyout.Items.Clear();

            foreach (var module in catalog.Modules)
            {
                var sub = new MenuFlyoutSubItem
                {
                    Text = ResourceHelper.GetString(module.DisplayResourceKey),
                };

                if (!string.IsNullOrEmpty(module.IconGlyph))
                {
                    sub.Icon = new FontIcon
                    {
                        Glyph = module.IconGlyph,
                        FontFamily = new FontFamily("Segoe Fluent Icons"),
                    };
                }

                foreach (var cmd in module.Commands)
                {
                    var item = new MenuFlyoutItem
                    {
                        Text = ResourceHelper.GetString(cmd.DisplayResourceKey),
                        Tag = cmd.Id,
                    };
                    item.Click += OnCommandPicked;
                    sub.Items.Add(item);
                }

                flyout.Items.Add(sub);
            }
        }

        private void OnCommandPicked(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string templateId)
            {
                ViewModel.SelectTemplate(templateId);
                MissingTemplateInfoBar.IsOpen = false;
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void ShowMissingTemplateInfoBar()
        {
            MissingTemplateInfoBar.IsOpen = true;
        }

        private void MissingTemplateChooseButton_Click(object sender, RoutedEventArgs e)
        {
            MissingTemplateInfoBar.IsOpen = false;
            TemplatePickerButton.Flyout.ShowAt(TemplatePickerButton);
        }

        private void MissingTemplateKeepButton_Click(object sender, RoutedEventArgs e)
        {
            MissingTemplateInfoBar.IsOpen = false;
            MissingTemplateKeepRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
