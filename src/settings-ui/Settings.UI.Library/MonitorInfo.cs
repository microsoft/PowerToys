// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using PowerDisplay.Common.Drivers;
using PowerDisplay.Common.Interfaces;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Utils;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class MonitorInfo : Observable, IMonitorData
    {
        private string _name = string.Empty;
        private string _internalName = string.Empty;
        private string _hardwareId = string.Empty;
        private string _communicationMethod = string.Empty;
        private int _currentBrightness;
        private int _colorTemperatureVcp = 0x05; // Default to 6500K preset (VCP 0x14 value)
        private int _contrast;
        private int _volume;
        private bool _isHidden;
        private bool _enableContrast;
        private bool _enableVolume;
        private string _capabilitiesRaw = string.Empty;
        private List<string> _vcpCodes = new List<string>();
        private List<VcpCodeDisplayInfo> _vcpCodesFormatted = new List<VcpCodeDisplayInfo>();
        private int _monitorNumber;
        private int _orientation;

        // Feature support status (determined from capabilities)
        private bool _supportsBrightness = true; // Brightness always shown even if unsupported
        private bool _supportsContrast;
        private bool _supportsColorTemperature;
        private bool _supportsVolume;
        private string _capabilitiesStatus = "unknown"; // "available", "unavailable", or "unknown"

        // Cached color temperature presets (computed from VcpCodesFormatted)
        private ObservableCollection<ColorPresetItem> _availableColorPresetsCache;
        private ObservableCollection<ColorPresetItem> _colorPresetsForDisplayCache;
        private int _lastColorTemperatureVcpForCache = -1;

        // Batch update support to reduce PropertyChanged notifications during bulk updates
        private bool _suppressNotifications;
        private bool _hasPendingNotifications;

        /// <summary>
        /// Suspends PropertyChanged notifications until the returned object is disposed.
        /// When disposed, a single PropertyChanged with empty string is raised to refresh all bindings.
        /// Use this when updating multiple properties at once to improve UI performance.
        /// </summary>
        /// <example>
        /// using (monitor.SuspendNotifications())
        /// {
        ///     monitor.CurrentBrightness = newBrightness;
        ///     monitor.ColorTemperatureVcp = newTemp;
        /// }  // Single UI refresh here
        /// </example>
        public IDisposable SuspendNotifications()
        {
            _suppressNotifications = true;
            _hasPendingNotifications = false;
            return new NotificationResumer(this);
        }

        /// <summary>
        /// Helper class to resume notifications when disposed.
        /// </summary>
        private sealed class NotificationResumer : IDisposable
        {
            private readonly MonitorInfo _owner;
            private bool _disposed;

            public NotificationResumer(MonitorInfo owner) => _owner = owner;

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _owner._suppressNotifications = false;

                // If any property changed during suspension, raise a single notification to refresh all
                if (_owner._hasPendingNotifications)
                {
                    _owner._hasPendingNotifications = false;
                    _owner.OnPropertyChanged(string.Empty);
                }
            }
        }

        /// <summary>
        /// Raises PropertyChanged if notifications are not suppressed.
        /// Tracks pending notifications during batch updates.
        /// </summary>
        protected new void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            if (_suppressNotifications)
            {
                _hasPendingNotifications = true;
                return;
            }

            base.OnPropertyChanged(propertyName);
        }

        /// <summary>
        /// Invalidates the color preset cache and notifies property changes.
        /// Call this when VcpCodesFormatted or SupportsColorTemperature changes.
        /// </summary>
        private void InvalidateColorPresetCache()
        {
            _availableColorPresetsCache = null;
            _colorPresetsForDisplayCache = null;
            _lastColorTemperatureVcpForCache = -1;
            OnPropertyChanged(nameof(AvailableColorPresets));
            OnPropertyChanged(nameof(ColorPresetsForDisplay));
        }

        /// <summary>
        /// Parses a hexadecimal string (with or without "0x" prefix) to an integer.
        /// </summary>
        private static bool TryParseHexCode(string hexString, out int result)
        {
            result = 0;
            if (string.IsNullOrEmpty(hexString))
            {
                return false;
            }

            var cleanHex = hexString.Replace("0x", string.Empty);
            return int.TryParse(cleanHex, System.Globalization.NumberStyles.HexNumber, null, out result);
        }

        public MonitorInfo()
        {
        }

        public MonitorInfo(string name, string internalName, string communicationMethod)
        {
            Name = name;
            InternalName = internalName;
            CommunicationMethod = communicationMethod;
        }

        public MonitorInfo(string name, string internalName, string hardwareId, string communicationMethod, int currentBrightness, int colorTemperatureVcp)
        {
            Name = name;
            InternalName = internalName;
            HardwareId = hardwareId;
            CommunicationMethod = communicationMethod;
            CurrentBrightness = currentBrightness;
            ColorTemperatureVcp = colorTemperatureVcp;
        }

        [JsonPropertyName("name")]
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        public string MonitorIconGlyph => CommunicationMethod.Contains("WMI", StringComparison.OrdinalIgnoreCase) == true
    ? "\uE7F8" // Laptop icon for WMI
    : "\uE7F4"; // External monitor icon for DDC/CI and others

        [JsonPropertyName("internalName")]
        public string InternalName
        {
            get => _internalName;
            set
            {
                if (_internalName != value)
                {
                    _internalName = value;
                    OnPropertyChanged();
                }
            }
        }

        [JsonPropertyName("communicationMethod")]
        public string CommunicationMethod
        {
            get => _communicationMethod;
            set
            {
                if (_communicationMethod != value)
                {
                    _communicationMethod = value;
                    OnPropertyChanged();
                }
            }
        }

        [JsonPropertyName("hardwareId")]
        public string HardwareId
        {
            get => _hardwareId;
            set
            {
                if (_hardwareId != value)
                {
                    _hardwareId = value;
                    OnPropertyChanged();
                }
            }
        }

        [JsonPropertyName("currentBrightness")]
        public int CurrentBrightness
        {
            get => _currentBrightness;
            set
            {
                if (_currentBrightness != value)
                {
                    _currentBrightness = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the color temperature VCP preset value (raw DDC/CI value from VCP code 0x14).
        /// This stores the raw VCP value (e.g., 0x05 for 6500K preset), not the Kelvin temperature.
        /// Use MonitorValueConverter to convert to human-readable Kelvin values for display.
        /// </summary>
        [JsonPropertyName("colorTemperatureVcp")]
        public int ColorTemperatureVcp
        {
            get => _colorTemperatureVcp;
            set
            {
                if (_colorTemperatureVcp != value)
                {
                    _colorTemperatureVcp = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ColorTemperatureDisplay));
                    OnPropertyChanged(nameof(ColorPresetsForDisplay)); // Update display list when current value changes
                }
            }
        }

        /// <summary>
        /// Gets the color temperature as a human-readable display string.
        /// Converts the VCP value to a Kelvin temperature display.
        /// </summary>
        [JsonIgnore]
        public string ColorTemperatureDisplay => MonitorValueConverter.FormatColorTemperatureDisplay(ColorTemperatureVcp);

        /// <summary>
        /// Gets or sets the current contrast value (0-100).
        /// </summary>
        [JsonPropertyName("contrast")]
        public int Contrast
        {
            get => _contrast;
            set
            {
                if (_contrast != value)
                {
                    _contrast = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the current volume value (0-100).
        /// </summary>
        [JsonPropertyName("volume")]
        public int Volume
        {
            get => _volume;
            set
            {
                if (_volume != value)
                {
                    _volume = value;
                    OnPropertyChanged();
                }
            }
        }

        [JsonPropertyName("isHidden")]
        public bool IsHidden
        {
            get => _isHidden;
            set
            {
                if (_isHidden != value)
                {
                    _isHidden = value;
                    OnPropertyChanged();
                }
            }
        }

        [JsonPropertyName("enableContrast")]
        public bool EnableContrast
        {
            get => _enableContrast;
            set
            {
                if (_enableContrast != value)
                {
                    _enableContrast = value;
                    OnPropertyChanged();
                }
            }
        }

        [JsonPropertyName("enableVolume")]
        public bool EnableVolume
        {
            get => _enableVolume;
            set
            {
                if (_enableVolume != value)
                {
                    _enableVolume = value;
                    OnPropertyChanged();
                }
            }
        }

        [JsonPropertyName("capabilitiesRaw")]
        public string CapabilitiesRaw
        {
            get => _capabilitiesRaw;
            set
            {
                if (_capabilitiesRaw != value)
                {
                    _capabilitiesRaw = value ?? string.Empty;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasCapabilities));
                }
            }
        }

        [JsonPropertyName("vcpCodes")]
        public List<string> VcpCodes
        {
            get => _vcpCodes;
            set
            {
                if (_vcpCodes != value)
                {
                    _vcpCodes = value ?? new List<string>();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(VcpCodesSummary));
                }
            }
        }

        [JsonPropertyName("vcpCodesFormatted")]
        public List<VcpCodeDisplayInfo> VcpCodesFormatted
        {
            get => _vcpCodesFormatted;
            set
            {
                var newValue = value ?? new List<VcpCodeDisplayInfo>();

                // Only update if content actually changed (compare by VCP code list content)
                if (AreVcpCodesEqual(_vcpCodesFormatted, newValue))
                {
                    return;
                }

                _vcpCodesFormatted = newValue;
                OnPropertyChanged();
                InvalidateColorPresetCache();
            }
        }

        /// <summary>
        /// Compare two VcpCodesFormatted lists for equality by content.
        /// Returns true if both lists have the same VCP codes (by code value).
        /// </summary>
        private static bool AreVcpCodesEqual(List<VcpCodeDisplayInfo> list1, List<VcpCodeDisplayInfo> list2)
        {
            if (list1 == null && list2 == null)
            {
                return true;
            }

            if (list1 == null || list2 == null)
            {
                return false;
            }

            if (list1.Count != list2.Count)
            {
                return false;
            }

            // Compare by code values - order matters for our use case
            for (int i = 0; i < list1.Count; i++)
            {
                if (list1[i].Code != list2[i].Code)
                {
                    return false;
                }

                // Also compare ValueList count to detect preset changes
                var values1 = list1[i].ValueList;
                var values2 = list2[i].ValueList;
                if ((values1?.Count ?? 0) != (values2?.Count ?? 0))
                {
                    return false;
                }
            }

            return true;
        }

        [JsonIgnore]
        public string VcpCodesSummary
        {
            get
            {
                if (_vcpCodes == null || _vcpCodes.Count == 0)
                {
                    return "No VCP codes detected";
                }

                var count = _vcpCodes.Count;
                var preview = string.Join(", ", _vcpCodes.Take(10));
                return count > 10
                    ? $"{count} VCP codes: {preview}..."
                    : $"{count} VCP codes: {preview}";
            }
        }

        [JsonPropertyName("supportsBrightness")]
        public bool SupportsBrightness
        {
            get => _supportsBrightness;
            set
            {
                if (_supportsBrightness != value)
                {
                    _supportsBrightness = value;
                    OnPropertyChanged();
                }
            }
        }

        [JsonPropertyName("supportsContrast")]
        public bool SupportsContrast
        {
            get => _supportsContrast;
            set
            {
                if (_supportsContrast != value)
                {
                    _supportsContrast = value;
                    OnPropertyChanged();
                }
            }
        }

        [JsonPropertyName("supportsColorTemperature")]
        public bool SupportsColorTemperature
        {
            get => _supportsColorTemperature;
            set
            {
                if (_supportsColorTemperature != value)
                {
                    _supportsColorTemperature = value;
                    OnPropertyChanged();
                    InvalidateColorPresetCache(); // Notifies AvailableColorPresets and ColorPresetsForDisplay
                }
            }
        }

        [JsonPropertyName("supportsVolume")]
        public bool SupportsVolume
        {
            get => _supportsVolume;
            set
            {
                if (_supportsVolume != value)
                {
                    _supportsVolume = value;
                    OnPropertyChanged();
                }
            }
        }

        [JsonPropertyName("capabilitiesStatus")]
        public string CapabilitiesStatus
        {
            get => _capabilitiesStatus;
            set
            {
                if (_capabilitiesStatus != value)
                {
                    _capabilitiesStatus = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ShowCapabilitiesWarning));
                }
            }
        }

        /// <summary>
        /// Available color temperature presets computed from VcpCodesFormatted (VCP code 0x14).
        /// This is a computed property that parses the VCP capabilities data on-demand.
        /// </summary>
        [JsonIgnore]
        public ObservableCollection<ColorPresetItem> AvailableColorPresets
        {
            get
            {
                // Return cached value if available
                if (_availableColorPresetsCache != null)
                {
                    return _availableColorPresetsCache;
                }

                // Compute from VcpCodesFormatted
                _availableColorPresetsCache = ComputeAvailableColorPresets();
                return _availableColorPresetsCache;
            }
        }

        /// <summary>
        /// Compute available color presets from VcpCodesFormatted (VCP code 0x14).
        /// Uses ColorTemperatureHelper from PowerDisplay.Lib for shared computation logic.
        /// </summary>
        private ObservableCollection<ColorPresetItem> ComputeAvailableColorPresets()
        {
            // Check if color temperature is supported
            if (!_supportsColorTemperature || _vcpCodesFormatted == null)
            {
                return new ObservableCollection<ColorPresetItem>();
            }

            // Find VCP code 0x14 (Color Temperature / Select Color Preset)
            var colorTempVcp = _vcpCodesFormatted.FirstOrDefault(v =>
                TryParseHexCode(v.Code, out int code) && code == NativeConstants.VcpCodeSelectColorPreset);

            // No VCP 0x14 or no values
            if (colorTempVcp == null || colorTempVcp.ValueList == null || colorTempVcp.ValueList.Count == 0)
            {
                return new ObservableCollection<ColorPresetItem>();
            }

            // Extract VCP values as tuples for ColorTemperatureHelper
            var colorTempValues = colorTempVcp.ValueList
                .Select(valueInfo =>
                {
                    bool parsed = TryParseHexCode(valueInfo.Value, out int vcpValue);
                    return (VcpValue: parsed ? vcpValue : 0, Name: valueInfo.Name);
                })
                .Where(x => x.VcpValue > 0);

            // Use shared helper to compute presets, then convert to nested type for XAML compatibility
            var basePresets = ColorTemperatureHelper.ComputeColorPresets(colorTempValues);
            var presetList = basePresets.Select(p => new ColorPresetItem(p.VcpValue, p.DisplayName));
            return new ObservableCollection<ColorPresetItem>(presetList);
        }

        /// <summary>
        /// Color presets for display in ComboBox, includes current value if not in preset list.
        /// Uses caching to avoid recreating collections on every access.
        /// </summary>
        [JsonIgnore]
        public ObservableCollection<ColorPresetItem> ColorPresetsForDisplay
        {
            get
            {
                // Return cached value if available and color temperature hasn't changed
                if (_colorPresetsForDisplayCache != null && _lastColorTemperatureVcpForCache == _colorTemperatureVcp)
                {
                    return _colorPresetsForDisplayCache;
                }

                var presets = AvailableColorPresets;
                if (presets == null || presets.Count == 0)
                {
                    _colorPresetsForDisplayCache = new ObservableCollection<ColorPresetItem>();
                    _lastColorTemperatureVcpForCache = _colorTemperatureVcp;
                    return _colorPresetsForDisplayCache;
                }

                // Check if current value is in the preset list
                var currentValueInList = presets.Any(p => p.VcpValue == _colorTemperatureVcp);

                if (currentValueInList)
                {
                    // Current value is in the list, return as-is
                    _colorPresetsForDisplayCache = presets;
                }
                else
                {
                    // Current value is not in the preset list - add it at the beginning
                    var displayList = new List<ColorPresetItem>();

                    // Add current value with "Custom" indicator using shared helper
                    var displayName = ColorTemperatureHelper.FormatCustomColorTemperatureDisplayName(_colorTemperatureVcp);
                    displayList.Add(new ColorPresetItem(_colorTemperatureVcp, displayName));

                    // Add all supported presets
                    displayList.AddRange(presets);

                    _colorPresetsForDisplayCache = new ObservableCollection<ColorPresetItem>(displayList);
                }

                _lastColorTemperatureVcpForCache = _colorTemperatureVcp;
                return _colorPresetsForDisplayCache;
            }
        }

        [JsonIgnore]
        public bool HasColorPresets => AvailableColorPresets != null && AvailableColorPresets.Count > 0;

        [JsonIgnore]
        public bool HasCapabilities => !string.IsNullOrEmpty(_capabilitiesRaw);

        [JsonIgnore]
        public bool ShowCapabilitiesWarning => _capabilitiesStatus == "unavailable";

        [JsonIgnore]
        public string BrightnessTooltip => _supportsBrightness ? string.Empty : "Brightness control not supported by this monitor";

        [JsonIgnore]
        public string ContrastTooltip => _supportsContrast ? string.Empty : "Contrast control not supported by this monitor";

        /// <summary>
        /// Generate formatted text of all VCP codes for clipboard
        /// </summary>
        public string GetVcpCodesAsText()
        {
            if (_vcpCodesFormatted == null || _vcpCodesFormatted.Count == 0)
            {
                return "No VCP codes detected";
            }

            var lines = new List<string>();
            lines.Add($"VCP Capabilities for: {_name}");
            lines.Add($"Hardware ID: {_hardwareId}");
            lines.Add(string.Empty);
            lines.Add("Detected VCP Codes:");
            lines.Add(new string('-', 50));

            foreach (var vcp in _vcpCodesFormatted)
            {
                lines.Add(string.Empty);
                lines.Add(vcp.Title);
                if (vcp.HasValues)
                {
                    lines.Add($"  {vcp.Values}");
                }
            }

            lines.Add(string.Empty);
            lines.Add(new string('-', 50));
            lines.Add($"Total: {_vcpCodesFormatted.Count} VCP codes");

            return string.Join(System.Environment.NewLine, lines);
        }

        /// <summary>
        /// Update this monitor's properties from another MonitorInfo instance.
        /// This preserves the object reference while updating all properties.
        /// </summary>
        /// <param name="other">The source MonitorInfo to copy properties from</param>
        public void UpdateFrom(MonitorInfo other)
        {
            if (other == null)
            {
                return;
            }

            // Update all properties that can change
            Name = other.Name;
            InternalName = other.InternalName;
            HardwareId = other.HardwareId;
            CommunicationMethod = other.CommunicationMethod;
            CurrentBrightness = other.CurrentBrightness;
            Contrast = other.Contrast;
            Volume = other.Volume;
            ColorTemperatureVcp = other.ColorTemperatureVcp;
            IsHidden = other.IsHidden;
            EnableContrast = other.EnableContrast;
            EnableVolume = other.EnableVolume;
            CapabilitiesRaw = other.CapabilitiesRaw;
            VcpCodes = other.VcpCodes;
            VcpCodesFormatted = other.VcpCodesFormatted;
            SupportsBrightness = other.SupportsBrightness;
            SupportsContrast = other.SupportsContrast;
            SupportsColorTemperature = other.SupportsColorTemperature;
            SupportsVolume = other.SupportsVolume;
            CapabilitiesStatus = other.CapabilitiesStatus;
        }

        /// <inheritdoc />
        string IMonitorData.Id
        {
            get => InternalName;
            set => InternalName = value;
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
            get => Contrast;
            set => Contrast = value;
        }

        /// <inheritdoc />
        int IMonitorData.Volume
        {
            get => Volume;
            set => Volume = value;
        }

        /// <inheritdoc />
        int IMonitorData.ColorTemperatureVcp
        {
            get => ColorTemperatureVcp;
            set => ColorTemperatureVcp = value;
        }

        /// <inheritdoc />
        int IMonitorData.MonitorNumber
        {
            get => _monitorNumber;
            set => _monitorNumber = value;
        }

        /// <inheritdoc />
        int IMonitorData.Orientation
        {
            get => _orientation;
            set => _orientation = value;
        }

        /// <summary>
        /// Type alias for ColorPresetItem to maintain backward compatibility with XAML bindings.
        /// Inherits from PowerDisplay.Common.Models.ColorPresetItem.
        /// </summary>
        public class ColorPresetItem : PowerDisplay.Common.Models.ColorPresetItem
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ColorPresetItem"/> class.
            /// </summary>
            public ColorPresetItem()
                : base()
            {
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="ColorPresetItem"/> class.
            /// </summary>
            /// <param name="vcpValue">The VCP value for the color temperature preset.</param>
            /// <param name="displayName">The display name for UI.</param>
            public ColorPresetItem(int vcpValue, string displayName)
                : base(vcpValue, displayName)
            {
            }
        }
    }
}
