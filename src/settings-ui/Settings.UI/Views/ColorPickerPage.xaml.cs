// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Input;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.Resources;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class ColorPickerPage : Page
    {
        public ColorPickerViewModel ViewModel { get; set; }

        public ICommand AddCommand => new RelayCommand(Add);

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

        private void Add()
        {
            ColorFormatModel newColorFormat = ColorFormatDialog.DataContext as ColorFormatModel;
            ViewModel.AddNewColorFormat(newColorFormat.Name, newColorFormat.Example, true);
            ColorFormatDialog.Hide();
        }

        public static string GetStringRepresentation(Color? color, string formatString)
        {
            if (color == null)
            {
                color = Color.Moccasin;
            }

            // convert all %?? expressions to strings
            int formatterPosition = formatString.IndexOf('%', 0);
            while (formatterPosition != -1)
            {
                if (formatterPosition >= formatString.Length - 1)
                {
                    // the formatter % was the last character, we are done
                    break;
                }

                char paramFormat = formatString[formatterPosition + 1];
                char paramType;
                int paramCount = 2;
                if (paramFormat >= '1' && paramFormat <= '9')
                {
                    // no parameter formatter, just param type defined. (like %2). Using the default formatter -> decimal
                    paramType = paramFormat;
                    paramFormat = 'd';
                    paramCount = 1; // we have only one parameter after the formatter char
                }
                else
                {
                    // need to check the next char, which should be between 1 and 9. Plus the parameter formatter should be valid.
                    if (formatterPosition >= formatString.Length - 2)
                    {
                        // not enough characters, end of string, we are done
                        break;
                    }

                    paramType = formatString[formatterPosition + 2];
                }

                if (paramType >= '1' && paramType <= '9' &&
                    (paramFormat == 'd' || paramFormat == 'p' || paramFormat == 'h' || paramFormat == 'f'))
                {
                    formatString = string.Concat(formatString.AsSpan(0, formatterPosition), GetStringRepresentation(color.Value, paramFormat, paramType), formatString.AsSpan(formatterPosition + paramCount + 1));
                }

                // search for the next occurence of the formatter char
                formatterPosition = formatString.IndexOf('%', formatterPosition + 1);
            }

            return formatString;
        }

        private static string GetStringRepresentation(Color color, char paramFormat, char paramType)
        {
            if (paramType < '1' || paramType > '9' || (paramFormat != 'd' && paramFormat != 'p' && paramFormat != 'h' && paramFormat != 'f'))
            {
                return string.Empty;
            }

            switch (paramType)
            {
                case '1': return color.R.ToString(CultureInfo.InvariantCulture);
                case '2': return color.G.ToString(CultureInfo.InvariantCulture);
                case '3': return color.B.ToString(CultureInfo.InvariantCulture);
                case '4': return color.A.ToString(CultureInfo.InvariantCulture);
                default: return string.Empty;
            }
        }

        private async void NewFormatClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var resourceLoader = ResourceLoader.GetForViewIndependentUse();
            ColorFormatDialog.Title = "Add custom color format"; // resourceLoader.GetString("AddNewEntryDialog_Title");
            ColorFormatDialog.DataContext = new ColorFormatModel();
            NewColorFormat.Description = GetStringRepresentation(null, NewColorFormat.Text);
            ColorFormatDialog.PrimaryButtonText = "Save"; // resourceLoader.GetString("AddBtn");
            ColorFormatDialog.PrimaryButtonCommand = AddCommand;
            await ColorFormatDialog.ShowAsync();
        }

        private void ColorFormatDialog_CancelButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            ColorFormatDialog.Hide();
        }

        private void NewColorFormat_TextChanged(object sender, TextChangedEventArgs e)
        {
            NewColorFormat.Description = GetStringRepresentation(null, NewColorFormat.Text);
        }
    }
}
