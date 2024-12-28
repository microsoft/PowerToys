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
        private PluginMetadataViewModel(string value, PluginMetadataType type)
        {
            _value = value;
            _type = type;
        }

        private string _value;

        public string Value => _value;

        private PluginMetadataType _type;

        public PluginMetadataType Type => _type;

        // Do not user for item separator initialization.
        public static PluginMetadataViewModel MetadataItem(string value, PluginMetadataType type)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(value);
            if (type == PluginMetadataType.ItemSeparator)
            {
                throw new ArgumentOutOfRangeException(nameof(type));
            }

            return new PluginMetadataViewModel(value, type);
        }

        public static PluginMetadataViewModel ItemSeparator()
        {
            return new PluginMetadataViewModel(string.Empty, PluginMetadataType.ItemSeparator);
        }

        // Handle property changes
        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Type enum
        public enum PluginMetadataType
        {
            Version,
            Author,
            Link,
            ItemSeparator,
        }
    }
}
