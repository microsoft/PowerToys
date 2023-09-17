// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.ComponentModel;
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

        // Labels of main (checkbox) setting
        public string DisplayLabel => _additionalOption.DisplayLabel;

        public string DisplayDescription => _additionalOption.DisplayDescription;

        // Labels of second setting (ComboBox, TextBox, NumberBox) - If only non-checkbox setting is shown we use the normal display labels.
        public string SecondDisplayLabel => (int)_additionalOption.PluginOptionType > 10 ? _additionalOption.SecondDisplayLabel : _additionalOption.DisplayLabel;

        public string SecondDisplayDescription => (int)_additionalOption.PluginOptionType > 10 ? _additionalOption.SecondDisplayDescription : _additionalOption.DisplayDescription;

        // Bool checkbox setting
        public bool ShowCheckBox => _additionalOption.PluginOptionType == PluginAdditionalOption.AdditionalOptionType.Checkbox || (int)_additionalOption.PluginOptionType > 10;

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
        public bool ShowComboBox => (_additionalOption.PluginOptionType == PluginAdditionalOption.AdditionalOptionType.Combobox || _additionalOption.PluginOptionType == PluginAdditionalOption.AdditionalOptionType.CheckboxAndCombobox) &&
            _additionalOption.ComboBoxOptions != null && _additionalOption.ComboBoxOptions.Count > 0;

        public List<string> ComboBoxOptions => _additionalOption.ComboBoxOptions;

        public int ComboBoxValue
        {
            get => _additionalOption.ComboBoxValue;
            set
            {
                if (value != _additionalOption.ComboBoxValue)
                {
                    _additionalOption.ComboBoxValue = value;
                    NotifyPropertyChanged();
                }
            }
        }

        // TextBox setting
        public bool ShowTextBox => _additionalOption.PluginOptionType == PluginAdditionalOption.AdditionalOptionType.Textbox || _additionalOption.PluginOptionType == PluginAdditionalOption.AdditionalOptionType.CheckboxAndTextbox;

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
        public bool ShowNumberBox => _additionalOption.PluginOptionType == PluginAdditionalOption.AdditionalOptionType.Numberbox || _additionalOption.PluginOptionType == PluginAdditionalOption.AdditionalOptionType.CheckboxAndNumberbox;

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
