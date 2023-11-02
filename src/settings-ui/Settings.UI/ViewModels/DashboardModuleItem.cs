// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Xaml;
using Windows.UI;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name
    public class DashboardModuleTextItem : DashboardModuleItem
    {
    }

    public class DashboardModuleButtonItem : DashboardModuleItem
    {
        public string ButtonTitle { get; set; }

        public bool IsButtonDescriptionVisible { get; set; }

        public string ButtonDescription { get; set; }

        public string ButtonGlyph { get; set; }

        public RoutedEventHandler ButtonClickHandler { get; set; }
    }

    public class DashboardModuleShortcutItem : DashboardModuleItem
    {
        public List<object> Shortcut { get; set; }
    }

    public class DashboardModuleKBMItem : DashboardModuleItem
    {
        private List<KeysDataModel> _remapKeys = new List<KeysDataModel>();

        public List<KeysDataModel> RemapKeys
        {
            get => _remapKeys;
            set => _remapKeys = value;
        }

        private List<AppSpecificKeysDataModel> _remapShortcuts = new List<AppSpecificKeysDataModel>();

        public List<AppSpecificKeysDataModel> RemapShortcuts
        {
            get => _remapShortcuts;
            set => _remapShortcuts = value;
        }
    }

    public class DashboardModuleItem : INotifyPropertyChanged
    {
        public string Label { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        internal void NotifyPropertyChanged(string propertyName)
        {
            OnPropertyChanged(propertyName);
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
#pragma warning restore SA1402 // File may only contain a single type
#pragma warning restore SA1649 // File name should match first type name
