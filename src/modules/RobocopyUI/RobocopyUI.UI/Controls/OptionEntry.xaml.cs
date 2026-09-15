// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RobocopyUI.Models;

namespace RobocopyUI.Controls
{
    public sealed partial class OptionEntry : UserControl
    {
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

        public static readonly DependencyProperty IsStorageOptionProperty =
            DependencyProperty.Register("IsStorageOption", typeof(bool), typeof(OptionEntry), new PropertyMetadata(false));

        public static readonly DependencyProperty IsTextOptionProperty =
            DependencyProperty.Register("IsTextOption", typeof(bool), typeof(OptionEntry), new PropertyMetadata(false));

        public static readonly DependencyProperty IsRunHoursOptionProperty =
            DependencyProperty.Register("IsRunHoursOption", typeof(bool), typeof(OptionEntry), new PropertyMetadata(false));

        public static readonly DependencyProperty MultiSelectOptionsProperty =
            DependencyProperty.Register("MultiSelectOptions", typeof(List<OptionContent>), typeof(OptionEntry), new PropertyMetadata(null));

        public static readonly DependencyProperty IsNumberOptionProperty =
            DependencyProperty.Register("IsNumberOption", typeof(bool), typeof(OptionEntry), new PropertyMetadata(false));

        public static readonly DependencyProperty IsMultiSelectOptionProperty =
            DependencyProperty.Register("IsMultiSelectOption", typeof(bool), typeof(OptionEntry), new PropertyMetadata(false));

        public static readonly DependencyProperty OptionDescriptionProperty =
            DependencyProperty.Register("OptionDescription", typeof(string), typeof(OptionEntry), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty OptionNameProperty =
            DependencyProperty.Register("OptionName", typeof(string), typeof(OptionEntry), new PropertyMetadata(string.Empty));

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

        public string GetCommandLine()
        {
            if (OptionEnabledCheckBox.IsChecked == true)
            {
                if (IsNumberOption)
                {
                    if (IsStorageOption)
                    {
                        return $"{OptionName}:{OptionNumberValue.Value}{((string)((ComboBoxItem)StorageUnitComboBox.SelectedItem).Content)[0]}";
                    }

                    return $"{OptionName}:{OptionNumberValue.Value}";
                }

                if (IsTextOption)
                {
                    return $"{OptionName}:{OptionTextValue.Text}";
                }

                if (IsMultiSelectOption)
                {
                    return $"{OptionName}:{string.Join(string.Empty, MultiSelectOptions.Where(o => o.Enabled).Select(o => o.OptionName))}";
                }

                if (IsRunHoursOption)
                {
                    return $"{OptionName}:{(int)StartHourNumberBox.Value:D2}{(int)StartMinuteNumberBox.Value:D2}-{(int)EndHourNumberBox.Value:D2}{(int)EndMinuteNumberBox.Value:D2}";
                }

                return $"{OptionName}";
            }

            return string.Empty;
        }
    }
}
