// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.WinUI3.Views
{
    public sealed partial class ColorPickerPage : Page
    {
        public ColorPickerViewModel ViewModel { get; set; }

        public ColorPickerPage()
        {
            var settingsUtils = new SettingsUtils();
            ViewModel = new ColorPickerViewModel(settingsUtils, SettingsRepository<GeneralSettings>.GetInstance(settingsUtils), ShellPage.SendDefaultIPCMessage);
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
            var index = 0;

            foreach (var item in ViewModel.SelectableColorRepresentations)
            {
                if (item.Key == ViewModel.SelectedColorRepresentationValue)
                {
                    break;
                }

                index++;
            }

            ColorPicker_ComboBox.SelectedIndex = index;
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
                ViewModel.ColorFormats.Move(index, index - 1);
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
                ViewModel.ColorFormats.Move(index, index + 1);
            }
        }
    }
}
