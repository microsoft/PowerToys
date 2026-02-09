// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.PowerToys.Settings.UI.Library;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>
    /// ViewModel for monitor selection in profile editor
    /// </summary>
    public class MonitorSelectionItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        private int _brightness = 100;
        private int _contrast = 50;
        private int _volume = 50;
        private int _colorTemperature = 6500;
        private bool _includeBrightness;
        private bool _includeContrast;
        private bool _includeVolume;
        private bool _includeColorTemperature;

        public required MonitorInfo Monitor { get; set; }

        public bool SuppressAutoSelection { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public int Brightness
        {
            get => _brightness;
            set
            {
                if (_brightness != value)
                {
                    _brightness = value;
                    OnPropertyChanged();
                    if (!SuppressAutoSelection)
                    {
                        IncludeBrightness = true;
                    }
                }
            }
        }

        public int Contrast
        {
            get => _contrast;
            set
            {
                if (_contrast != value)
                {
                    _contrast = value;
                    OnPropertyChanged();
                    if (!SuppressAutoSelection)
                    {
                        IncludeContrast = true;
                    }
                }
            }
        }

        public int Volume
        {
            get => _volume;
            set
            {
                if (_volume != value)
                {
                    _volume = value;
                    OnPropertyChanged();
                    if (!SuppressAutoSelection)
                    {
                        IncludeVolume = true;
                    }
                }
            }
        }

        public int ColorTemperature
        {
            get => _colorTemperature;
            set
            {
                if (_colorTemperature != value)
                {
                    _colorTemperature = value;
                    OnPropertyChanged();
                    if (!SuppressAutoSelection)
                    {
                        IncludeColorTemperature = true;
                    }
                }
            }
        }

        public bool SupportsContrast => Monitor?.SupportsContrast ?? false;

        public bool SupportsVolume => Monitor?.SupportsVolume ?? false;

        public bool SupportsColorTemperature => Monitor?.SupportsColorTemperature ?? false;

        public bool IncludeBrightness
        {
            get => _includeBrightness;
            set
            {
                if (_includeBrightness != value)
                {
                    _includeBrightness = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IncludeContrast
        {
            get => _includeContrast;
            set
            {
                if (_includeContrast != value)
                {
                    _includeContrast = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IncludeVolume
        {
            get => _includeVolume;
            set
            {
                if (_includeVolume != value)
                {
                    _includeVolume = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IncludeColorTemperature
        {
            get => _includeColorTemperature;
            set
            {
                if (_includeColorTemperature != value)
                {
                    _includeColorTemperature = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
