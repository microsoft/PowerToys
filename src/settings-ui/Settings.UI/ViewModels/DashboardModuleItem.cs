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
    public partial class DashboardModuleTextItem : DashboardModuleItem
    {
    }

    public partial class DashboardModuleButtonItem : DashboardModuleItem
    {
        public string ButtonTitle { get; set; }

        public bool IsButtonDescriptionVisible { get; set; }

        public string ButtonDescription { get; set; }

        public string ButtonGlyph { get; set; }

        public RoutedEventHandler ButtonClickHandler { get; set; }
    }

    public partial class DashboardModuleShortcutItem : DashboardModuleItem
    {
        private List<object> _shortcut;

        public List<object> Shortcut
        {
            get => _shortcut;
            set
            {
                if (_shortcut != value)
                {
                    _shortcut = value;
                    NotifyPropertyChanged(nameof(Shortcut));
                }
            }
        }
    }

    public partial class DashboardModuleActivationItem : DashboardModuleItem
    {
        private string _activation;

        public string Activation
        {
            get => _activation;
            set
            {
                if (_activation != value)
                {
                    _activation = value;
                    NotifyPropertyChanged(nameof(Activation));
                }
            }
        }
    }

    public partial class DashboardModuleItem : INotifyPropertyChanged
    {
        private string _label;

        public string Label
        {
            get => _label;
            set
            {
                if (_label != value)
                {
                    _label = value;
                    OnPropertyChanged();
                }
            }
        }

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
