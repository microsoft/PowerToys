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
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Utils;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class MonitorInfo : Observable
    {
        private string _name = string.Empty;
        private string _id = string.Empty;
        private string _communicationMethod = string.Empty;
        private int _currentBrightness;
        private int _colorTemperatureVcp = 0x05; // Default to 6500K preset (VCP 0x14 value)
        private int _contrast;
        private int _volume;
        private bool _isHidden;
        private bool _enableContrast;
        private bool _enableVolume;
        private bool _enableInputSource;
        private bool _enableRotation;
        private bool _enableColorTemperature;
        private string _capabilitiesRaw = string.Empty;
        private List<VcpCodeDisplayInfo> _vcpCodesFormatted = new List<VcpCodeDisplayInfo>();
        private int _monitorNumber;
        private int _totalMonitorCount;

        // Feature support status (determined from capabilities)
        private bool _supportsBrightness = true; // Brightness always shown even if unsupported
        private bool _supportsContrast;
        private bool _supportsColorTemperature;
        private bool _supportsVolume;
        private bool _supportsInputSource;

        // Cached color temperature presets (computed from VcpCodesFormatted)
        private ObservableCollection<ColorPresetItem> _availableColorPresetsCache;
        private ObservableCollection<ColorPresetItem> _colorPresetsForDisplayCache;
        private int _lastColorTemperatureVcpForCache = -1;

        /// <summary>
        /// Invalidates the color preset cache and notifies property changes.
        /// Call this when VcpCodesFormatted or SupportsColorTemperature changes.
        /// </summary>
        private void InvalidateColorPresetCache()
        {
            _availableColorPresetsCache = null;
            _colorPresetsForDisplayCache = null;
            _lastColorTemperatureVcpForCache = -1;
            OnPropertyChanged(nameof(ColorPresetsForDisplay));
        }

        public MonitorInfo()
        {
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
                    OnPropertyChanged(nameof(DisplayName));
                }
            }
        }

        /// <summary>
        /// Gets or sets the monitor number (Windows DISPLAY number, e.g., 1, 2, 3...).
        /// </summary>
        [JsonPropertyName("monitorNumber")]
        public int MonitorNumber
        {
            get => _monitorNumber;
            set
            {
                if (_monitorNumber != value)
                {
                    _monitorNumber = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayName));
                }
            }
        }

        /// <summary>
        /// Gets or sets the total number of monitors (used for dynamic display name).
        /// This is not serialized; it's set by the ViewModel.
        /// </summary>
        [JsonIgnore]
        public int TotalMonitorCount
        {
            get => _totalMonitorCount;
            set
            {
                if (_totalMonitorCount != value)
                {
                    _totalMonitorCount = value;
                    OnPropertyChanged(nameof(DisplayName));
                }
            }
        }

        /// <summary>
        /// Gets the display name - includes monitor number when multiple monitors exist.
        /// Follows the same logic as PowerDisplay UI's MonitorViewModel.DisplayName.
        /// </summary>
        [JsonIgnore]
        public string DisplayName
        {
            get
            {
                // Show monitor number only when there are multiple monitors and MonitorNumber is valid
                if (TotalMonitorCount > 1 && MonitorNumber > 0)
                {
                    return $"{Name} {MonitorNumber}";
                }

                return Name;
            }
        }

        public string MonitorIconGlyph => CommunicationMethod.Contains("WMI", StringComparison.OrdinalIgnoreCase)
            ? "\uE7F8" // Laptop icon for WMI
            : "\uE7F4"; // External monitor icon for DDC/CI and others

        [JsonPropertyName("id")]
        public string Id
        {
            get => _id;
            set
            {
                if (_id != value)
                {
                    _id = value;
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
                    OnPropertyChanged(nameof(ColorPresetsForDisplay)); // Update display list when current value changes
                }
            }
        }

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

        [JsonPropertyName("enableInputSource")]
        public bool EnableInputSource
        {
            get => _enableInputSource;
            set
            {
                if (_enableInputSource != value)
                {
                    _enableInputSource = value;
                    OnPropertyChanged();
                }
            }
        }

        [JsonPropertyName("enableRotation")]
        public bool EnableRotation
        {
            get => _enableRotation;
            set
            {
                if (_enableRotation != value)
                {
                    _enableRotation = value;
                    OnPropertyChanged();
                }
            }
        }

        [JsonPropertyName("enableColorTemperature")]
        public bool EnableColorTemperature
        {
            get => _enableColorTemperature;
            set
            {
                if (_enableColorTemperature != value)
                {
                    _enableColorTemperature = value;
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
                    InvalidateColorPresetCache(); // Notifies ColorPresetsForDisplay
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

        [JsonPropertyName("supportsInputSource")]
        public bool SupportsInputSource
        {
            get => _supportsInputSource;
            set
            {
                if (_supportsInputSource != value)
                {
                    _supportsInputSource = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets available color temperature presets computed from VcpCodesFormatted (VCP code 0x14).
        /// This is a computed property that parses the VCP capabilities data on-demand.
        /// </summary>
        private ObservableCollection<ColorPresetItem> AvailableColorPresets
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
                !string.IsNullOrEmpty(v.Code) &&
                int.TryParse(
                    v.Code.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? v.Code[2..] : v.Code,
                    System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out int code) &&
                code == NativeConstants.VcpCodeSelectColorPreset);

            // No VCP 0x14 or no values
            if (colorTempVcp == null || colorTempVcp.ValueList == null || colorTempVcp.ValueList.Count == 0)
            {
                return new ObservableCollection<ColorPresetItem>();
            }

            // Extract VCP values as tuples for ColorTemperatureHelper
            var colorTempValues = colorTempVcp.ValueList
                .Select(valueInfo =>
                {
                    var hex = valueInfo.Value;
                    if (string.IsNullOrEmpty(hex))
                    {
                        return (VcpValue: 0, Name: valueInfo.Name);
                    }

                    var cleanHex = hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? hex[2..] : hex;
                    bool parsed = int.TryParse(cleanHex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out int vcpValue);
                    return (VcpValue: parsed ? vcpValue : 0, Name: valueInfo.Name);
                })
                .Where(x => x.VcpValue > 0);

            // Use shared helper to compute presets, then convert to nested type for XAML compatibility
            var basePresets = ColorTemperatureHelper.ComputeColorPresets(colorTempValues);
            var presetList = basePresets.Select(p => new ColorPresetItem(p.VcpValue, p.DisplayName));
            return new ObservableCollection<ColorPresetItem>(presetList);
        }

        /// <summary>
        /// Gets color presets for display in ComboBox, includes current value if not in preset list.
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
        public bool HasCapabilities => !string.IsNullOrEmpty(_capabilitiesRaw);

        [JsonIgnore]
        public bool ShowCapabilitiesWarning => _communicationMethod.Contains("WMI", StringComparison.OrdinalIgnoreCase);

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
            lines.Add($"Monitor ID: {_id}");
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
            Id = other.Id;
            CommunicationMethod = other.CommunicationMethod;
            CurrentBrightness = other.CurrentBrightness;
            Contrast = other.Contrast;
            Volume = other.Volume;
            ColorTemperatureVcp = other.ColorTemperatureVcp;
            IsHidden = other.IsHidden;
            EnableContrast = other.EnableContrast;
            EnableVolume = other.EnableVolume;
            EnableInputSource = other.EnableInputSource;
            EnableRotation = other.EnableRotation;
            EnableColorTemperature = other.EnableColorTemperature;
            CapabilitiesRaw = other.CapabilitiesRaw;
            VcpCodesFormatted = other.VcpCodesFormatted;
            SupportsBrightness = other.SupportsBrightness;
            SupportsContrast = other.SupportsContrast;
            SupportsColorTemperature = other.SupportsColorTemperature;
            SupportsVolume = other.SupportsVolume;
            SupportsInputSource = other.SupportsInputSource;
            MonitorNumber = other.MonitorNumber;
        }
    }
}
