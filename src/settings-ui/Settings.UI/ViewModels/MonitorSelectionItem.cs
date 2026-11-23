// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerDisplay.Models;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>
    /// ViewModel for monitor selection in profile editor
    /// </summary>
    public class MonitorSelectionItem : INotifyPropertyChanged, IDisposable
    {
        private bool _isSelected;
        private int _brightness = 100;
        private int _contrast = 50;
        private int _volume = 50;
        private int? _colorTemperature;
        private bool _includeBrightness;
        private bool _includeContrast;
        private bool _includeVolume;
        private bool _includeColorTemperature;
        private bool _hasOriginalBrightness;
        private bool _hasOriginalContrast;
        private bool _hasOriginalVolume;
        private int? _originalColorTemperature;
        private bool _isColorTemperatureEdited;

        public MonitorSelectionItem(MonitorInfo monitor)
        {
            ArgumentNullException.ThrowIfNull(monitor);
            Monitor = monitor;
            Monitor.PropertyChanged += OnMonitorPropertyChanged;
        }

        public MonitorInfo Monitor { get; }

        public ObservableCollection<ColorPresetItem> ColorPresetsForDisplay => Monitor.ColorPresetsForDisplay;

        public bool HasValidColorTemperature => SupportsColorTemperature && IsColorTemperatureAvailable(_colorTemperature);

        public bool HasValidSettings =>
            (IncludeBrightness || ProfileContrast.HasValue || ProfileVolume.HasValue || ProfileColorTemperature.HasValue) &&
            (!IncludeColorTemperature || ProfileColorTemperature.HasValue || (!SupportsColorTemperature && !_isColorTemperatureEdited));

        public bool HasPreservedSettings =>
            (IncludeBrightness && _hasOriginalBrightness && !SupportsBrightness) ||
            (IncludeContrast && _hasOriginalContrast && !SupportsContrast) ||
            (IncludeVolume && _hasOriginalVolume && !SupportsVolume) ||
            (IncludeColorTemperature && CanPreserveColorTemperature && !HasValidColorTemperature);

        public bool ShowBrightness => SupportsBrightness || IncludeBrightness;

        public bool ShowContrast => SupportsContrast || IncludeContrast;

        public bool ShowVolume => SupportsVolume || IncludeVolume;

        public bool ShowColorTemperature => SupportsColorTemperature || IncludeColorTemperature;

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

        public int? ColorTemperature
        {
            get => _colorTemperature;
            set
            {
                // Preserve an existing profile value until the user edits this field. A
                // refresh can clear the ComboBox without representing a user edit.
                bool edited = !SuppressAutoSelection && value != _colorTemperature;
                bool editStateChanged = edited && !_isColorTemperatureEdited;
                _isColorTemperatureEdited |= edited;
                var selection = IsColorTemperatureAvailable(value) ? value : null;
                if (_colorTemperature != selection)
                {
                    _colorTemperature = selection;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasValidColorTemperature));
                    NotifyProfileStateChanged();
                }
                else if (editStateChanged)
                {
                    NotifyProfileStateChanged();
                }

                if (edited && value.HasValue)
                {
                    IncludeColorTemperature = true;
                }
            }
        }

        public bool SupportsBrightness => Monitor.SupportsBrightness;

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
                    OnPropertyChanged(nameof(ShowBrightness));
                    NotifyProfileStateChanged();
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
                    OnPropertyChanged(nameof(ShowContrast));
                    NotifyProfileStateChanged();
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
                    OnPropertyChanged(nameof(ShowVolume));
                    NotifyProfileStateChanged();
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
                    // Opting into a currently available color value is a new choice,
                    // even when the ComboBox still shows the monitor's current value.
                    if (value && !SuppressAutoSelection && SupportsColorTemperature && !_originalColorTemperature.HasValue)
                    {
                        _isColorTemperatureEdited = true;
                    }

                    _includeColorTemperature = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ShowColorTemperature));
                    NotifyProfileStateChanged();
                }
            }
        }

        private bool CanPreserveColorTemperature => _originalColorTemperature.HasValue && !_isColorTemperatureEdited;

        private int? ProfileContrast => IncludeContrast && (SupportsContrast || _hasOriginalContrast) ? Contrast : null;

        private int? ProfileVolume => IncludeVolume && (SupportsVolume || _hasOriginalVolume) ? Volume : null;

        private int? ProfileColorTemperature => !IncludeColorTemperature ? null
            : CanPreserveColorTemperature ? _originalColorTemperature
            : HasValidColorTemperature ? ColorTemperature : null;

        public event PropertyChangedEventHandler? PropertyChanged;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        internal void LoadProfileSetting(ProfileMonitorSetting? setting)
        {
            var wasSuppressed = SuppressAutoSelection;
            SuppressAutoSelection = true;
            try
            {
                _hasOriginalBrightness = setting?.Brightness.HasValue ?? false;
                _hasOriginalContrast = setting?.Contrast.HasValue ?? false;
                _hasOriginalVolume = setting?.Volume.HasValue ?? false;
                _originalColorTemperature = setting?.ColorTemperatureVcp;
                _isColorTemperatureEdited = false;
                IsSelected = setting != null;
                Brightness = setting?.Brightness ?? Monitor.CurrentBrightness;
                Contrast = setting?.Contrast ?? 50;
                Volume = setting?.Volume ?? 50;
                ColorTemperature = setting?.ColorTemperatureVcp ?? Monitor.ColorTemperatureVcp;
                IncludeBrightness = _hasOriginalBrightness;
                IncludeContrast = _hasOriginalContrast;
                IncludeVolume = _hasOriginalVolume;
                IncludeColorTemperature = _originalColorTemperature.HasValue;
            }
            finally
            {
                SuppressAutoSelection = wasSuppressed;
            }

            NotifyProfileStateChanged();
        }

        internal ProfileMonitorSetting CreateProfileSetting(string monitorId)
        {
            return new ProfileMonitorSetting(
                monitorId,
                IncludeBrightness ? Brightness : null,
                ProfileColorTemperature,
                ProfileContrast,
                ProfileVolume);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Monitor.PropertyChanged -= OnMonitorPropertyChanged;
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool IsColorTemperatureAvailable(int? value)
        {
            return value.HasValue && ColorPresetsForDisplay.Any(preset => preset.VcpValue == value.Value);
        }

        private void OnMonitorPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MonitorInfo.SupportsBrightness) ||
                e.PropertyName == nameof(MonitorInfo.SupportsContrast) ||
                e.PropertyName == nameof(MonitorInfo.SupportsVolume) ||
                e.PropertyName == nameof(MonitorInfo.SupportsColorTemperature))
            {
                OnPropertyChanged(e.PropertyName);
                OnPropertyChanged(nameof(ShowBrightness));
                OnPropertyChanged(nameof(ShowContrast));
                OnPropertyChanged(nameof(ShowVolume));
                OnPropertyChanged(nameof(ShowColorTemperature));
                NotifyProfileStateChanged();
                return;
            }

            if (e.PropertyName != nameof(MonitorInfo.ColorPresetsForDisplay))
            {
                return;
            }

            var selection = _colorTemperature ?? (CanPreserveColorTemperature ? _originalColorTemperature : null);
            var wasSuppressed = SuppressAutoSelection;
            SuppressAutoSelection = true;
            try
            {
                // Replacing ItemsSource can synchronously clear SelectedValue. Forward
                // the list change here so that neither that clear nor restoring a valid
                // selection is treated as the user opting into color temperature.
                OnPropertyChanged(nameof(ColorPresetsForDisplay));
                ColorTemperature = selection;
                OnPropertyChanged(nameof(ColorTemperature));
                OnPropertyChanged(nameof(HasValidColorTemperature));
                NotifyProfileStateChanged();
            }
            finally
            {
                SuppressAutoSelection = wasSuppressed;
            }
        }

        private void NotifyProfileStateChanged()
        {
            OnPropertyChanged(nameof(HasValidSettings));
            OnPropertyChanged(nameof(HasPreservedSettings));
        }
    }
}
