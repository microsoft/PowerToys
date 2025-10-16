// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;

namespace PowerDisplay.Core.Models
{
    /// <summary>
    /// Monitor model that implements property change notification
    /// Thread-safe using Interlocked operations for concurrent access
    /// </summary>
    public class Monitor : INotifyPropertyChanged
    {
        private int _currentBrightness;
        private int _currentColorTemperature = 6500;
        private int _isAvailable = 1; // 1 = available, 0 = unavailable (for Interlocked)

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
        /// Thread-safe using Interlocked.Exchange
        /// </summary>
        public int CurrentBrightness
        {
            get => Volatile.Read(ref _currentBrightness);
            set
            {
                var clamped = Math.Clamp(value, MinBrightness, MaxBrightness);
                var oldValue = Interlocked.Exchange(ref _currentBrightness, clamped);
                if (oldValue != clamped)
                {
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
        /// Thread-safe using Interlocked.Exchange
        /// </summary>
        public int CurrentColorTemperature
        {
            get => Volatile.Read(ref _currentColorTemperature);
            set
            {
                var clamped = Math.Clamp(value, MinColorTemperature, MaxColorTemperature);
                var oldValue = Interlocked.Exchange(ref _currentColorTemperature, clamped);
                if (oldValue != clamped)
                {
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
        /// Thread-safe using Interlocked.Exchange
        /// </summary>
        public int CurrentContrast
        {
            get => Volatile.Read(ref _currentContrast);
            set
            {
                var clamped = Math.Clamp(value, MinContrast, MaxContrast);
                var oldValue = Interlocked.Exchange(ref _currentContrast, clamped);
                if (oldValue != clamped)
                {
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
        /// Thread-safe using Interlocked.Exchange
        /// </summary>
        public int CurrentVolume
        {
            get => Volatile.Read(ref _currentVolume);
            set
            {
                var clamped = Math.Clamp(value, MinVolume, MaxVolume);
                var oldValue = Interlocked.Exchange(ref _currentVolume, clamped);
                if (oldValue != clamped)
                {
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
        /// Thread-safe using Interlocked.Exchange
        /// </summary>
        public bool IsAvailable
        {
            get => Volatile.Read(ref _isAvailable) == 1;
            set
            {
                var newValue = value ? 1 : 0;
                var oldValue = Interlocked.Exchange(ref _isAvailable, newValue);
                if (oldValue != newValue)
                {
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