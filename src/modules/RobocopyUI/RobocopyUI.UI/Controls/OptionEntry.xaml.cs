// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RobocopyUI.Models;

namespace RobocopyUI.Controls
{
    public sealed partial class OptionEntry : UserControl
    {
        public event EventHandler<object, EventArgs>? OptionChanged;

        public string OptionName
        {
            get { return (string)GetValue(OptionNameProperty); }
            set { SetValue(OptionNameProperty, value); }
        }

        public string OptionDescription
        {
            get { return (string)GetValue(OptionDescriptionProperty); }
            set { SetValue(OptionDescriptionProperty, value); }
        }

        public bool IsNumberOption
        {
            get { return (bool)GetValue(IsNumberOptionProperty); }
            set { SetValue(IsNumberOptionProperty, value); }
        }

        public bool IsMultiSelectOption
        {
            get { return (bool)GetValue(IsMultiSelectOptionProperty); }
            set { SetValue(IsMultiSelectOptionProperty, value); }
        }

        public List<OptionContent> MultiSelectOptions
        {
            get { return (List<OptionContent>)GetValue(MultiSelectOptionsProperty); }
            set { SetValue(MultiSelectOptionsProperty, value); }
        }

        public bool IsRunHoursOption
        {
            get { return (bool)GetValue(IsRunHoursOptionProperty); }
            set { SetValue(IsRunHoursOptionProperty, value); }
        }

        public bool IsStorageOption
        {
            get { return (bool)GetValue(IsStorageOptionProperty); }
            set { SetValue(IsStorageOptionProperty, value); }
        }

        public bool IsTextOption
        {
            get { return (bool)GetValue(IsTextOptionProperty); }
            set { SetValue(IsTextOptionProperty, value); }
        }

        public bool IsSelected
        {
            get { return OptionEnabledCheckBox.IsChecked == true; }
            set { OptionEnabledCheckBox.IsChecked = value; }
        }

        public double NumberValue
        {
            get { return OptionNumberValue.Value; }
            set { OptionNumberValue.Value = value; }
        }

        public string TextValue
        {
            get { return OptionTextValue.Text; }
            set { OptionTextValue.Text = value; }
        }

        public int StartHour
        {
            get { return (int)StartHourNumberBox.Value; }
            set { StartHourNumberBox.Value = value; }
        }

        public int StartMinute
        {
            get { return (int)StartMinuteNumberBox.Value; }
            set { StartMinuteNumberBox.Value = value; }
        }

        public int EndHour
        {
            get { return (int)EndHourNumberBox.Value; }
            set { EndHourNumberBox.Value = value; }
        }

        public int EndMinute
        {
            get { return (int)EndMinuteNumberBox.Value; }
            set { EndMinuteNumberBox.Value = value; }
        }

        public string StorageUnit
        {
            get
            {
                return (string)((ComboBoxItem)StorageUnitComboBox.SelectedItem).Content;
            }

            set
            {
                foreach (object item in StorageUnitComboBox.Items)
                {
                    if ((string)((ComboBoxItem)item).Content == value)
                    {
                        StorageUnitComboBox.SelectedItem = item;
                        break;
                    }
                }
            }
        }

        public string SelectedItems
        {
            set
            {
                if (IsMultiSelectOption && MultiSelectOptions != null)
                {
                    foreach (var option in MultiSelectOptions)
                    {
                        option.Enabled = value.Contains(option.OptionName);
                    }
                }
            }
        }

        public string CommandLineContent
        {
            get { return (string)GetValue(CommandLineContentProperty); }
            private set { SetValue(CommandLineContentProperty, value); }
        }

        public static readonly DependencyProperty IsStorageOptionProperty =
            DependencyProperty.Register(nameof(OptionEntry.IsStorageOption), typeof(bool), typeof(OptionEntry), new PropertyMetadata(false));

        public static readonly DependencyProperty IsTextOptionProperty =
            DependencyProperty.Register(nameof(OptionEntry.IsTextOption), typeof(bool), typeof(OptionEntry), new PropertyMetadata(false));

        public static readonly DependencyProperty IsRunHoursOptionProperty =
            DependencyProperty.Register(nameof(OptionEntry.IsRunHoursOption), typeof(bool), typeof(OptionEntry), new PropertyMetadata(false));

        public static readonly DependencyProperty MultiSelectOptionsProperty =
            DependencyProperty.Register(nameof(OptionEntry.MultiSelectOptions), typeof(List<OptionContent>), typeof(OptionEntry), new PropertyMetadata(null));

        public static readonly DependencyProperty IsNumberOptionProperty =
            DependencyProperty.Register(nameof(OptionEntry.IsNumberOption), typeof(bool), typeof(OptionEntry), new PropertyMetadata(false));

        public static readonly DependencyProperty IsMultiSelectOptionProperty =
            DependencyProperty.Register(nameof(OptionEntry.IsMultiSelectOption), typeof(bool), typeof(OptionEntry), new PropertyMetadata(false));

        public static readonly DependencyProperty OptionDescriptionProperty =
            DependencyProperty.Register(nameof(OptionEntry.OptionDescription), typeof(string), typeof(OptionEntry), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty OptionNameProperty =
            DependencyProperty.Register(nameof(OptionEntry.OptionName), typeof(string), typeof(OptionEntry), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty CommandLineContentProperty =
            DependencyProperty.Register(nameof(OptionEntry.CommandLineContent), typeof(string), typeof(OptionEntry), new PropertyMetadata(string.Empty));

        public OptionEntry()
        {
            MultiSelectOptions ??= [];
            InitializeComponent();

            if (App.Options.TryGetValue(OptionName, out var optionContent))
            {
                OptionEnabledCheckBox.IsChecked = optionContent.Enabled;
                OptionNumberValue.Value = optionContent.IntValue;
            }
        }

        private void OptionCheckedUnchecked(object sender, RoutedEventArgs e)
        {
            CommandLineContent = GetCommandLine();
            OptionChanged?.Invoke(this, EventArgs.Empty);
        }

        public string GetCommandLine()
        {
            var value = string.Empty;

            if (OptionEnabledCheckBox.IsChecked == true)
            {
                if (IsNumberOption)
                {
                    value = IsStorageOption
                        ? $"{OptionName}:{OptionNumberValue.Value}{((string)((ComboBoxItem)StorageUnitComboBox.SelectedItem).Content)[0]}"
                        : $"{OptionName}:{OptionNumberValue.Value}";
                }
                else if (IsTextOption)
                {
                    value = $"{OptionName}:{OptionTextValue.Text}";
                }
                else if (IsMultiSelectOption)
                {
                    value = $"{OptionName}:{string.Join(string.Empty, MultiSelectOptions.Where(o => o.Enabled).Select(o => o.OptionName))}";
                }
                else if (IsRunHoursOption)
                {
                    value = $"{OptionName}:{(int)StartHourNumberBox.Value:D2}{(int)StartMinuteNumberBox.Value:D2}-{(int)EndHourNumberBox.Value:D2}{(int)EndMinuteNumberBox.Value:D2}";
                }
                else
                {
                    value = $"{OptionName}";
                }
            }

            return value;
        }
    }
}
