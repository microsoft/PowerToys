// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using PowerDisplay.Common.Interfaces;
using PowerDisplay.Common.Utils;

namespace PowerDisplay.Common.Models
{
    /// <summary>
    /// Monitor model that implements property change notification.
    /// Implements IMonitorData to provide a common interface for monitor hardware values.
    /// </summary>
    public partial class Monitor : INotifyPropertyChanged, IMonitorData
    {
        private int _currentBrightness;
        private int _currentColorTemperature = 0x05; // Default to 6500K preset (VCP 0x14 value)
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
        /// Current brightness (0-100)
        /// </summary>
        public int CurrentBrightness
        {
            get => _currentBrightness;
            set
            {
                var clamped = Math.Clamp(value, MinBrightness, MaxBrightness);
                if (_currentBrightness != clamped)
                {
                    _currentBrightness = clamped;
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
        /// Current color temperature VCP preset value (from VCP code 0x14).
        /// This stores the raw VCP value (e.g., 0x05 for 6500K), not Kelvin temperature.
        /// Use ColorTemperaturePresetName to get human-readable name.
        /// </summary>
        public int CurrentColorTemperature
        {
            get => _currentColorTemperature;
            set
            {
                if (_currentColorTemperature != value)
                {
                    _currentColorTemperature = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ColorTemperaturePresetName));
                }
            }
        }

        /// <summary>
        /// Human-readable color temperature preset name (e.g., "6500K (0x05)", "sRGB (0x01)")
        /// </summary>
        public string ColorTemperaturePresetName =>
            VcpValueNames.GetFormattedName(0x14, CurrentColorTemperature);

        /// <summary>
        /// Whether supports color temperature adjustment via VCP 0x14
        /// </summary>
        public bool SupportsColorTemperature { get; set; }

        /// <summary>
        /// Capabilities detection status: "available", "unavailable", or "unknown"
        /// </summary>
        public string CapabilitiesStatus { get; set; } = "unknown";

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
                var clamped = Math.Clamp(value, MinContrast, MaxContrast);
                if (_currentContrast != clamped)
                {
                    _currentContrast = clamped;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Minimum contrast value
        /// </summary>
        public int MinContrast { get; set; }

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
                var clamped = Math.Clamp(value, MinVolume, MaxVolume);
                if (_currentVolume != clamped)
                {
                    _currentVolume = clamped;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Minimum volume value
        /// </summary>
        public int MinVolume { get; set; }

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
        /// Device key - unique identifier part of device path
        /// </summary>
        public string DeviceKey { get; set; } = string.Empty;

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
        /// Raw DDC/CI capabilities string (MCCS format)
        /// </summary>
        public string? CapabilitiesRaw { get; set; }

        /// <summary>
        /// Parsed VCP capabilities information
        /// </summary>
        public VcpCapabilities? VcpCapabilitiesInfo { get; set; }

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
            return $"{Name} ({CommunicationMethod}) - {CurrentBrightness}%";
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

        /// <inheritdoc />
        int IMonitorData.Brightness
        {
            get => CurrentBrightness;
            set => CurrentBrightness = value;
        }

        /// <inheritdoc />
        int IMonitorData.Contrast
        {
            get => CurrentContrast;
            set => CurrentContrast = value;
        }

        /// <inheritdoc />
        int IMonitorData.Volume
        {
            get => CurrentVolume;
            set => CurrentVolume = value;
        }

        /// <inheritdoc />
        int IMonitorData.ColorTemperatureVcp
        {
            get => CurrentColorTemperature;
            set => CurrentColorTemperature = value;
        }
    }
}
