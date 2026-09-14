// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace RobocopyUI
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

        public List<OptionEntry> MultiSelectOptions
        {
            get { return (List<OptionEntry>)GetValue(MultiSelectOptionsProperty); }
            set { SetValue(MultiSelectOptionsProperty, value); }
        }

        public bool IsRunHoursOption
        {
            get { return (bool)GetValue(IsRunHoursOptionProperty); }
            set { SetValue(IsRunHoursOptionProperty, value); }
        }

        public static readonly DependencyProperty IsRunHoursOptionProperty =
            DependencyProperty.Register("IsRunHoursOption", typeof(bool), typeof(OptionEntry), new PropertyMetadata(false));

        public static readonly DependencyProperty MultiSelectOptionsProperty =
            DependencyProperty.Register("MultiSelectOptions", typeof(List<OptionEntry>), typeof(OptionEntry), new PropertyMetadata(null));

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
            DataContext = this;

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
                    return $"{OptionName}:{OptionNumberValue.Value}";
                }

                if (IsMultiSelectOption)
                {
                    return $"{OptionName}:{string.Join(string.Empty, MultiSelectOptions.Where(o => o.OptionEnabledCheckBox.IsChecked ?? false).Select(o => o.OptionName))}";
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
