// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.PowerToys.Settings.UI.Library;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class PluginAdditionalOptionViewModel : INotifyPropertyChanged
    {
        private PluginAdditionalOption _additionalOption;

        internal PluginAdditionalOptionViewModel(PluginAdditionalOption additionalOption)
        {
            _additionalOption = additionalOption;
        }

        // Labels of single and first setting of combined types
        public string DisplayLabel => _additionalOption.DisplayLabel;

        public string DisplayDescription => _additionalOption.DisplayDescription;

        // Labels of second setting of combined types
        public string SecondDisplayLabel => _additionalOption.SecondDisplayLabel;

        public string SecondDisplayDescription => _additionalOption.SecondDisplayDescription;

        // Bool checkbox setting
        public bool ShowCheckBox => _additionalOption.PluginOptionType == PluginAdditionalOption.AdditionalOptionType.Checkbox;

        public bool Value
        {
            get => _additionalOption.Value;
            set
            {
                if (value != _additionalOption.Value)
                {
                    _additionalOption.Value = value;
                    NotifyPropertyChanged();
                    NotifyPropertyChanged(nameof(SecondSettingIsEnabled));
                }
            }
        }

        // ComboBox setting
        public bool ShowComboBox => _additionalOption.PluginOptionType == PluginAdditionalOption.AdditionalOptionType.Combobox &&
            _additionalOption.ComboBoxItems != null && _additionalOption.ComboBoxItems.Count > 0;

        public List<KeyValuePair<string, string>> ComboBoxItems => _additionalOption.ComboBoxItems;

        public string ComboBoxValue
        {
            get => _additionalOption.ComboBoxValue.ToString(CultureInfo.InvariantCulture);
            set
            {
                if (int.Parse(value, CultureInfo.InvariantCulture) != _additionalOption.ComboBoxValue)
                {
                    _additionalOption.ComboBoxValue = int.Parse(value, CultureInfo.InvariantCulture);
                    NotifyPropertyChanged();
                }
            }
        }

        // TextBox setting
        public bool ShowTextBox => _additionalOption.PluginOptionType == PluginAdditionalOption.AdditionalOptionType.Textbox;

        public int TextBoxMaxLength => (_additionalOption.TextBoxMaxLength == null) ? 0 : _additionalOption.TextBoxMaxLength.Value; // 0 is the default and means no limit.

        public string TextValue
        {
            get => _additionalOption.TextValue;
            set
            {
                if (value != _additionalOption.TextValue)
                {
                    _additionalOption.TextValue = value;
                    NotifyPropertyChanged();
                }
            }
        }

        // NumberBox setting
        public bool ShowNumberBox => _additionalOption.PluginOptionType == PluginAdditionalOption.AdditionalOptionType.Numberbox;

        public double NumberBoxMin => (_additionalOption.NumberBoxMin == null) ? double.MinValue : _additionalOption.NumberBoxMin.Value;

        public double NumberBoxMax => (_additionalOption.NumberBoxMax == null) ? double.MaxValue : _additionalOption.NumberBoxMax.Value;

        public double NumberBoxSmallChange => (_additionalOption.NumberBoxSmallChange == null) ? 1 : _additionalOption.NumberBoxSmallChange.Value;

        public double NumberBoxLargeChange => (_additionalOption.NumberBoxLargeChange == null) ? 10 : _additionalOption.NumberBoxLargeChange.Value;

        public double NumberValue
        {
            get => _additionalOption.NumberValue;
            set
            {
                if (value != _additionalOption.NumberValue)
                {
                    _additionalOption.NumberValue = value;
                    NotifyPropertyChanged();
                }
            }
        }

        // Show combined settings cards
        public bool ShowCheckboxAndCombobox => _additionalOption.PluginOptionType == PluginAdditionalOption.AdditionalOptionType.CheckboxAndCombobox;

        public bool ShowCheckboxAndTextbox => _additionalOption.PluginOptionType == PluginAdditionalOption.AdditionalOptionType.CheckboxAndTextbox;

        public bool ShowCheckboxAndNumberbox => _additionalOption.PluginOptionType == PluginAdditionalOption.AdditionalOptionType.CheckboxAndNumberbox;

        // Enabled state of ComboBox, TextBox, NumberBox (If combined with checkbox then checkbox value decides it.)
        public bool SecondSettingIsEnabled => (int)_additionalOption.PluginOptionType > 10 ? _additionalOption.Value : true;

        // Handle property changes
        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
