// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Windows.Input;

using CommunityToolkit.WinUI.Controls;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class ColorPickerPage : Page, IRefreshablePage
    {
        public ColorPickerViewModel ViewModel { get; set; }

        public ICommand AddCommand => new RelayCommand(Add);

        public ICommand UpdateCommand => new RelayCommand(Update);

        private ResourceLoader resourceLoader = ResourceLoaderInstance.ResourceLoader;

        public ColorPickerPage()
        {
            var settingsUtils = new SettingsUtils();
            ViewModel = new ColorPickerViewModel(
                settingsUtils,
                SettingsRepository<GeneralSettings>.GetInstance(settingsUtils),
                SettingsRepository<ColorPickerSettings>.GetInstance(settingsUtils),
                ShellPage.SendDefaultIPCMessage);
            DataContext = ViewModel;
            InitializeComponent();
        }

        /// <summary>
        /// Event is called when the <see cref="ComboBox"/> is completely loaded, inclusive the ItemSource
        /// </summary>
        /// <param name="sender">The sender of this event</param>
        /// <param name="e">The arguments of this event</param>
        private void ColorPicker_ComboBox_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            /**
             * UWP hack
             * because UWP load the bound ItemSource of the ComboBox asynchronous,
             * so after InitializeComponent() the ItemSource is still empty and can't automatically select a entry.
             * Selection via SelectedItem and SelectedValue is still not working too
             */
            ViewModel.SetPreviewSelectedIndex();
        }

        private void ReorderButtonUp_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            ColorFormatModel color = ((MenuFlyoutItem)sender).DataContext as ColorFormatModel;
            if (color == null)
            {
                return;
            }

            var index = ViewModel.ColorFormats.IndexOf(color);
            if (index > 0)
            {
                ViewModel.ColorFormats.Move(index, --index);
                SetColorFormatsFocus(index);
            }
        }

        private void ReorderButtonDown_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            ColorFormatModel color = ((MenuFlyoutItem)sender).DataContext as ColorFormatModel;
            if (color == null)
            {
                return;
            }

            var index = ViewModel.ColorFormats.IndexOf(color);
            if (index < ViewModel.ColorFormats.Count - 1)
            {
                ViewModel.ColorFormats.Move(index, ++index);
                SetColorFormatsFocus(index);
            }
        }

        private async void RemoveButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            ColorFormatModel color = ((MenuFlyoutItem)sender).DataContext as ColorFormatModel;

            ContentDialog dialog = new ContentDialog();
            dialog.XamlRoot = RootPage.XamlRoot;
            dialog.Title = color.Name;
            dialog.PrimaryButtonText = resourceLoader.GetString("Yes");
            dialog.CloseButtonText = resourceLoader.GetString("No");
            dialog.DefaultButton = ContentDialogButton.Primary;
            dialog.Content = new TextBlock() { Text = resourceLoader.GetString("Delete_Dialog_Description") };
            dialog.PrimaryButtonClick += (s, args) =>
            {
                var deleteIndex = ViewModel.DeleteModel(color);
                SetColorFormatsFocus(deleteIndex < ViewModel.ColorFormats.Count ? deleteIndex : ViewModel.ColorFormats.Count - 1);
            };
            var result = await dialog.ShowAsync();
        }

        private void Add()
        {
            ColorFormatModel newColorFormat = ColorFormatDialog.DataContext as ColorFormatModel;
            ViewModel.AddNewColorFormat(newColorFormat.Name, newColorFormat.Format, true);
            ColorFormatDialog.Hide();
            SetColorFormatsFocus(0);
        }

        private void Update()
        {
            ColorFormatModel colorFormat = ColorFormatDialog.DataContext as ColorFormatModel;
            string oldName = ((KeyValuePair<string, string>)ColorFormatDialog.Tag).Key;
            ViewModel.UpdateColorFormat(oldName, colorFormat);
        }

        private async void NewFormatClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            ColorFormatDialog.Title = resourceLoader.GetString("AddCustomColorFormat");
            ColorFormatModel newColorFormatModel = ViewModel.GetNewColorFormatModel();
            ColorFormatDialog.DataContext = newColorFormatModel;
            ColorFormatDialog.Tag = string.Empty;

            ColorFormatDialog.PrimaryButtonText = resourceLoader.GetString("ColorFormatSave");
            ColorFormatDialog.PrimaryButtonCommand = AddCommand;
            await ColorFormatDialog.ShowAsync();
        }

        private async void EditButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            SettingsCard btn = sender as SettingsCard;
            ColorFormatModel colorFormatModel = btn.DataContext as ColorFormatModel;
            ColorFormatDialog.Title = resourceLoader.GetString("EditCustomColorFormat");
            ColorFormatDialog.DataContext = colorFormatModel;
            ColorFormatDialog.Tag = new KeyValuePair<string, string>(colorFormatModel.Name, colorFormatModel.Format);

            ColorFormatDialog.PrimaryButtonText = resourceLoader.GetString("ColorFormatUpdate");
            ColorFormatDialog.PrimaryButtonCommand = UpdateCommand;
            await ColorFormatDialog.ShowAsync();
        }

        private void ColorFormatEditor_PropertyChanged(object sender, EventArgs e)
        {
            ColorFormatDialog.IsPrimaryButtonEnabled = ViewModel.SetValidity(ColorFormatDialog.DataContext as ColorFormatModel, ColorFormatDialog.Tag as string);
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }

        private void ColorFormatDialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            if (args.Result != ContentDialogResult.Primary && ColorFormatDialog.Tag is KeyValuePair<string, string>)
            {
                ColorFormatModel modifiedColorFormat = ColorFormatDialog.DataContext as ColorFormatModel;
                KeyValuePair<string, string> oldProperties = (KeyValuePair<string, string>)ColorFormatDialog.Tag;
                modifiedColorFormat.Name = oldProperties.Key;
                modifiedColorFormat.Format = oldProperties.Value;
            }
        }

        private void SetColorFormatsFocus(int index)
        {
            ColorFormats.UpdateLayout();
            var colorFormat = ColorFormats.ContainerFromIndex(index) as ContentPresenter;
            colorFormat.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
        }
    }
}
