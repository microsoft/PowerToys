// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

using ManagedCommon;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class FlyoutMenuItem : INotifyPropertyChanged
    {
        private bool _visible;
        private bool _isEnabled;

        public string Label { get; set; }

        public string Icon { get; set; }

        public string ToolTip { get; set; }

        public ModuleType Tag { get; set; }

        public bool IsLocked { get; set; }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnPropertyChanged();
                    EnabledChangedCallback?.Invoke(this);
                }
            }
        }

        public Action<FlyoutMenuItem> EnabledChangedCallback { get; set; }

        public bool Visible
        {
            get => _visible;
            set
            {
                if (_visible != value)
                {
                    _visible = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
