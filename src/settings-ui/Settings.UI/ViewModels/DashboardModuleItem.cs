// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Library;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class DashboardModuleItem : INotifyPropertyChanged
    {
        private bool _isLabelVisible;
        private bool _isShortcutVisible;
        private bool _isButtonVisible;

        public string Label { get; set; }

        public bool IsLabelVisible
        {
            get => _isLabelVisible;
            set
            {
                if (_isLabelVisible != value)
                {
                    _isLabelVisible = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsShortcutVisible
        {
            get => _isShortcutVisible;
            set
            {
                if (_isShortcutVisible != value)
                {
                    _isShortcutVisible = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsButtonVisible
        {
            get => _isButtonVisible;
            set
            {
                if (_isButtonVisible != value)
                {
                    _isButtonVisible = value;
                    OnPropertyChanged();
                }
            }
        }

        public List<object> Shortcut { get; set; }

        public string ButtonTitle { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
