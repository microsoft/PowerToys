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

        public string DisplayLabel => _additionalOption.DisplayLabel;

        public string DisplayDescription => _additionalOption.DisplayDescription;

        public bool Value
        {
            get => _additionalOption.Value;
            set
            {
                if (value != _additionalOption.Value)
                {
                    _additionalOption.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public List<string> ComboBoxOptions => _additionalOption.ComboBoxOptions;

        public int Option
        {
            get => _additionalOption.Option;
            set
            {
                if (value != _additionalOption.Option)
                {
                    _additionalOption.Option = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool ShowComboBox => _additionalOption.SelectionTypeValue == (int)PluginAdditionalOption.SelectionType.Combobox && _additionalOption.ComboBoxOptions != null && _additionalOption.ComboBoxOptions.Count > 0;

        public bool ShowCheckBox => _additionalOption.SelectionTypeValue == (int)PluginAdditionalOption.SelectionType.Checkbox;

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
