// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PowerDisplay.Core.Models
{
    /// <summary>
    /// Monitor model that implements property change notification
    /// </summary>
    public class Monitor : INotifyPropertyChanged
    {
        private int _currentBrightness;
        private int _currentColorTemperature = 6500;
        private bool _isAvailable = true;

        /// <summary>
        /// Unique identifier (based on hardware ID)
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Hardware ID (EDID format like GSM5C6D)
        /// </summary>
        public string HardwareId { get; set; } = string.Empty;

        /// <summary>
        /// Display name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Monitor type
        /// </summary>
        public MonitorType Type { get; set; } = MonitorType.Unknown;

        /// <summary>
        /// Current brightness (0-100)
        /// </summary>
        public int CurrentBrightness
        {
            get => _currentBrightness;
            set
            {
                if (_currentBrightness != value)
                {
                    _currentBrightness = Math.Clamp(value, MinBrightness, MaxBrightness);
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Minimum brightness value
        /// </summary>
        public int MinBrightness { get; set; }

        /// <summary>
        /// Maximum brightness value
        /// </summary>
        public int MaxBrightness { get; set; } = 100;

        /// <summary>
        /// Current color temperature (2000-10000K)
        /// </summary>
        public int CurrentColorTemperature
        {
            get => _currentColorTemperature;
            set
            {
                if (_currentColorTemperature != value)
                {
                    _currentColorTemperature = Math.Clamp(value, MinColorTemperature, MaxColorTemperature);
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Minimum color temperature value
        /// </summary>
        public int MinColorTemperature { get; set; } = 2000;

        /// <summary>
        /// Maximum color temperature value
        /// </summary>
        public int MaxColorTemperature { get; set; } = 10000;

        /// <summary>
        /// Whether supports color temperature adjustment
        /// </summary>
        public bool SupportsColorTemperature { get; set; } = true;

        /// <summary>
        /// Whether supports contrast adjustment
        /// </summary>
        public bool SupportsContrast => Capabilities.HasFlag(MonitorCapabilities.Contrast);

        /// <summary>
        /// Whether supports volume adjustment (for audio-capable monitors)
        /// </summary>
        public bool SupportsVolume => Capabilities.HasFlag(MonitorCapabilities.Volume);

        private int _currentContrast = 50;
        private int _currentVolume = 50;

        /// <summary>
        /// Current contrast (0-100)
        /// </summary>
        public int CurrentContrast
        {
            get => _currentContrast;
            set
            {
                if (_currentContrast != value)
                {
                    _currentContrast = Math.Clamp(value, MinContrast, MaxContrast);
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Minimum contrast value
        /// </summary>
        public int MinContrast { get; set; } = 0;

        /// <summary>
        /// Maximum contrast value
        /// </summary>
        public int MaxContrast { get; set; } = 100;

        /// <summary>
        /// Current volume (0-100)
        /// </summary>
        public int CurrentVolume
        {
            get => _currentVolume;
            set
            {
                if (_currentVolume != value)
                {
                    _currentVolume = Math.Clamp(value, MinVolume, MaxVolume);
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Minimum volume value
        /// </summary>
        public int MinVolume { get; set; } = 0;

        /// <summary>
        /// Maximum volume value
        /// </summary>
        public int MaxVolume { get; set; } = 100;

        /// <summary>
        /// Whether available/online
        /// </summary>
        public bool IsAvailable
        {
            get => _isAvailable;
            set
            {
                if (_isAvailable != value)
                {
                    _isAvailable = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Physical monitor handle (for DDC/CI)
        /// </summary>
        public IntPtr Handle { get; set; } = IntPtr.Zero;

        /// <summary>
        /// Device path (for identification)
        /// </summary>
        public string DevicePath { get; set; } = string.Empty;

        /// <summary>
        /// Device key - unique identifier part of device path (like Twinkle Tray's deviceKey)
        /// </summary>
        public string DeviceKey { get; set; } = string.Empty;

        /// <summary>
        /// Full device ID path (like Twinkle Tray's deviceID)
        /// </summary>
        public string DeviceID { get; set; } = string.Empty;

        /// <summary>
        /// Instance name (used by WMI)
        /// </summary>
        public string InstanceName { get; set; } = string.Empty;

        /// <summary>
        /// Manufacturer information
        /// </summary>
        public string Manufacturer { get; set; } = string.Empty;

        /// <summary>
        /// Connection type (HDMI, DP, VGA, etc.)
        /// </summary>
        public string ConnectionType { get; set; } = string.Empty;

        /// <summary>
        /// Communication method (DDC/CI, WMI, HDR API, etc.)
        /// </summary>
        public string CommunicationMethod { get; set; } = string.Empty;

        /// <summary>
        /// Supported control methods
        /// </summary>
        public MonitorCapabilities Capabilities { get; set; } = MonitorCapabilities.None;

        /// <summary>
        /// Last update time
        /// </summary>
        public DateTime LastUpdate { get; set; } = DateTime.Now;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override string ToString()
        {
            return $"{Name} ({Type}) - {CurrentBrightness}%";
        }

        /// <summary>
        /// Update monitor status
        /// </summary>
        public void UpdateStatus(int brightness, bool isAvailable = true)
        {
            IsAvailable = isAvailable;
            if (isAvailable)
            {
                CurrentBrightness = brightness;
                LastUpdate = DateTime.Now;
            }
        }
    }
}