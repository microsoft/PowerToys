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
    /// <remarks>
    /// <para><see cref="Id"/> is the unique identifier used for all purposes: UI lookups, IPC, persistent storage, and handle management.</para>
    /// <para>Format: "{Source}_{EdidId}_{MonitorNumber}" (e.g., "DDC_GSM5C6D_1", "WMI_BOE0900_2").</para>
    /// </remarks>
    public partial class Monitor : INotifyPropertyChanged, IMonitorData
    {
        private int _currentBrightness;
        private int _currentColorTemperature = 0x05; // Default to 6500K preset (VCP 0x14 value)
        private int _currentInputSource; // VCP 0x60 value
        private bool _isAvailable = true;

        /// <summary>
        /// Gets or sets unique identifier for all purposes: UI lookups, IPC, persistent storage, and handle management.
        /// </summary>
        /// <remarks>
        /// Format: "{Source}_{EdidId}_{MonitorNumber}" where Source is "DDC" or "WMI".
        /// Examples: "DDC_GSM5C6D_1", "WMI_BOE0900_2".
        /// Stable across reboots and unique even for multiple identical monitors.
        /// </remarks>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets display name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets current brightness (0-100)
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
        /// Gets or sets minimum brightness value
        /// </summary>
        public int MinBrightness { get; set; }

        /// <summary>
        /// Gets or sets maximum brightness value
        /// </summary>
        public int MaxBrightness { get; set; } = 100;

        /// <summary>
        /// Gets or sets current color temperature VCP preset value (from VCP code 0x14).
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
        /// Gets human-readable color temperature preset name (e.g., "6500K (0x05)", "sRGB (0x01)")
        /// </summary>
        public string ColorTemperaturePresetName =>
            VcpValueNames.GetFormattedName(0x14, CurrentColorTemperature);

        /// <summary>
        /// Gets or sets a value indicating whether whether supports color temperature adjustment via VCP 0x14
        /// </summary>
        public bool SupportsColorTemperature { get; set; }

        /// <summary>
        /// Gets or sets current input source VCP value (from VCP code 0x60).
        /// This stores the raw VCP value (e.g., 0x11 for HDMI-1).
        /// Use InputSourceName to get human-readable name.
        /// </summary>
        public int CurrentInputSource
        {
            get => _currentInputSource;
            set
            {
                if (_currentInputSource != value)
                {
                    _currentInputSource = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(InputSourceName));
                }
            }
        }

        /// <summary>
        /// Gets human-readable input source name (e.g., "HDMI-1", "DisplayPort-1")
        /// Returns just the name without hex value for cleaner UI display.
        /// </summary>
        public string InputSourceName =>
            VcpValueNames.GetName(0x60, CurrentInputSource) ?? $"Source 0x{CurrentInputSource:X2}";

        /// <summary>
        /// Gets a value indicating whether whether supports input source switching via VCP 0x60
        /// </summary>
        public bool SupportsInputSource => VcpCapabilitiesInfo?.SupportsVcpCode(0x60) ?? false;

        /// <summary>
        /// Gets get supported input sources from capabilities (as list of VCP values)
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<int>? SupportedInputSources =>
            VcpCapabilitiesInfo?.GetSupportedValues(0x60);

        /// <summary>
        /// Gets or sets capabilities detection status: "available", "unavailable", or "unknown"
        /// </summary>
        public string CapabilitiesStatus { get; set; } = "unknown";

        /// <summary>
        /// Gets a value indicating whether whether supports contrast adjustment
        /// </summary>
        public bool SupportsContrast => Capabilities.HasFlag(MonitorCapabilities.Contrast);

        /// <summary>
        /// Gets a value indicating whether whether supports volume adjustment (for audio-capable monitors)
        /// </summary>
        public bool SupportsVolume => Capabilities.HasFlag(MonitorCapabilities.Volume);

        private int _currentContrast = 50;
        private int _currentVolume = 50;

        /// <summary>
        /// Gets or sets current contrast (0-100)
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
        /// Gets or sets minimum contrast value
        /// </summary>
        public int MinContrast { get; set; }

        /// <summary>
        /// Gets or sets maximum contrast value
        /// </summary>
        public int MaxContrast { get; set; } = 100;

        /// <summary>
        /// Gets or sets current volume (0-100)
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
        /// Gets or sets minimum volume value
        /// </summary>
        public int MinVolume { get; set; }

        /// <summary>
        /// Gets or sets maximum volume value
        /// </summary>
        public int MaxVolume { get; set; } = 100;

        /// <summary>
        /// Gets or sets a value indicating whether whether available/online
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
        /// Gets or sets physical monitor handle (for DDC/CI)
        /// </summary>
        public IntPtr Handle { get; set; } = IntPtr.Zero;

        /// <summary>
        /// Gets or sets instance name (used by WMI)
        /// </summary>
        public string InstanceName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets communication method (DDC/CI, WMI, HDR API, etc.)
        /// </summary>
        public string CommunicationMethod { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets supported control methods
        /// </summary>
        public MonitorCapabilities Capabilities { get; set; } = MonitorCapabilities.None;

        /// <summary>
        /// Gets or sets raw DDC/CI capabilities string (MCCS format)
        /// </summary>
        public string? CapabilitiesRaw { get; set; }

        /// <summary>
        /// Gets or sets parsed VCP capabilities information
        /// </summary>
        public VcpCapabilities? VcpCapabilitiesInfo { get; set; }

        /// <summary>
        /// Gets or sets last update time
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

        /// <summary>
        /// Gets or sets monitor number (1, 2, 3...)
        /// </summary>
        public int MonitorNumber { get; set; }

        /// <summary>
        /// Gets or sets the GDI device name (e.g., "\\.\DISPLAY1").
        /// This is obtained from QueryDisplayConfig during discovery and should be used
        /// for display settings APIs (EnumDisplaySettings, ChangeDisplaySettingsEx).
        /// </summary>
        public string GdiDeviceName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets monitor orientation (0=0, 1=90, 2=180, 3=270)
        /// </summary>
        public int Orientation { get; set; }

        /// <inheritdoc />
        int IMonitorData.MonitorNumber
        {
            get => MonitorNumber;
            set => MonitorNumber = value;
        }

        /// <inheritdoc />
        int IMonitorData.Orientation
        {
            get => Orientation;
            set => Orientation = value;
        }
    }
}
