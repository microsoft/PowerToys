// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Microsoft.PowerToys.Settings.UI.Library.ViewModels
{
    public class PluginAdditionalOptionViewModel : INotifyPropertyChanged
    {
        private PluginAdditionalOption _additionalOption;

        internal PluginAdditionalOptionViewModel(PluginAdditionalOption additionalOption)
        {
            _additionalOption = additionalOption;
        }

        public string DisplayLabel { get => _additionalOption.DisplayLabel; }

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

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
