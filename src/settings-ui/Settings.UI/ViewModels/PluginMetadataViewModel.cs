// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class PluginMetadataViewModel : INotifyPropertyChanged
    {
        public PluginMetadataViewModel(string value, PluginMetadataType type)
        {
            _value = value;
            _type = type;
        }

        private string _value;

        private PluginMetadataType _type;

        public string Value => _value;

        public PluginMetadataType Type => _type;

        // Handle property changes
        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public enum PluginMetadataType
        {
            Version,
            Author,
            Link,
        }
    }
}
