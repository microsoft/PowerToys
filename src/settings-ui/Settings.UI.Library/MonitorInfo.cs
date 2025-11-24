// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class MonitorInfo : Observable
    {
        private string _name = string.Empty;
        private string _internalName = string.Empty;
        private string _hardwareId = string.Empty;
        private string _communicationMethod = string.Empty;
        private int _currentBrightness;
        private int _colorTemperature = 6500;
        private bool _isHidden;
        private bool _enableContrast;
        private bool _enableVolume;
        private string _capabilitiesRaw = string.Empty;
        private List<string> _vcpCodes = new List<string>();
        private List<VcpCodeDisplayInfo> _vcpCodesFormatted = new List<VcpCodeDisplayInfo>();

        // Feature support status (determined from capabilities)
        private bool _supportsBrightness = true; // Brightness always shown even if unsupported
        private bool _supportsContrast;
        private bool _supportsColorTemperature;
        private bool _supportsVolume;
        private string _capabilitiesStatus = "unknown"; // "available", "unavailable", or "unknown"

        // Cached color temperature presets (computed from VcpCodesFormatted)
        private ObservableCollection<ColorPresetItem> _availableColorPresetsCache;

        public MonitorInfo()
        {
        }

        public MonitorInfo(string name, string internalName, string communicationMethod)
        {
            Name = name;
            InternalName = internalName;
            CommunicationMethod = communicationMethod;
        }

        public MonitorInfo(string name, string internalName, string hardwareId, string communicationMethod, int currentBrightness, int colorTemperature)
        {
            Name = name;
            InternalName = internalName;
            HardwareId = hardwareId;
            CommunicationMethod = communicationMethod;
            CurrentBrightness = currentBrightness;
            ColorTemperature = colorTemperature;
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

        [JsonPropertyName("colorTemperature")]
        public int ColorTemperature
        {
            get => _colorTemperature;
            set
            {
                if (_colorTemperature != value)
                {
                    _colorTemperature = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ColorPresetsForDisplay)); // Update display list when current value changes
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
                if (_vcpCodesFormatted != value)
                {
                    _vcpCodesFormatted = value ?? new List<VcpCodeDisplayInfo>();
                    _availableColorPresetsCache = null; // Clear cache when VCP codes change
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AvailableColorPresets));
                }
            }
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
                    _availableColorPresetsCache = null; // Clear cache when support status changes
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AvailableColorPresets)); // Refresh computed property
                    OnPropertyChanged(nameof(ColorPresetsForDisplay)); // Refresh display list
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
                Logger.LogInfo($"[MonitorInfo.AvailableColorPresets] GET called for monitor '{_name}'");

                // Return cached value if available
                if (_availableColorPresetsCache != null)
                {
                    Logger.LogInfo($"[MonitorInfo.AvailableColorPresets] Cache HIT - returning {_availableColorPresetsCache.Count} items");
                    return _availableColorPresetsCache;
                }

                Logger.LogInfo("[MonitorInfo.AvailableColorPresets] Cache MISS - computing from VcpCodesFormatted");

                // Compute from VcpCodesFormatted
                _availableColorPresetsCache = ComputeAvailableColorPresets();

                Logger.LogInfo($"[MonitorInfo.AvailableColorPresets] Computed {_availableColorPresetsCache.Count} items");
                return _availableColorPresetsCache;
            }
        }

        /// <summary>
        /// Compute available color presets from VcpCodesFormatted (VCP code 0x14)
        /// </summary>
        private ObservableCollection<ColorPresetItem> ComputeAvailableColorPresets()
        {
            Logger.LogInfo($"[ComputeAvailableColorPresets] START for monitor '{_name}'");
            Logger.LogInfo($"  - SupportsColorTemperature: {_supportsColorTemperature}");
            Logger.LogInfo($"  - VcpCodesFormatted: {(_vcpCodesFormatted == null ? "NULL" : $"{_vcpCodesFormatted.Count} items")}");

            // Check if color temperature is supported
            if (!_supportsColorTemperature || _vcpCodesFormatted == null)
            {
                Logger.LogWarning($"[ComputeAvailableColorPresets] Color temperature not supported or no VCP codes - returning empty");
                return new ObservableCollection<ColorPresetItem>();
            }

            // Find VCP code 0x14 (Color Temperature / Select Color Preset)
            var colorTempVcp = _vcpCodesFormatted.FirstOrDefault(v =>
            {
                if (int.TryParse(v.Code?.Replace("0x", string.Empty), System.Globalization.NumberStyles.HexNumber, null, out int code))
                {
                    return code == 0x14;
                }

                return false;
            });

            Logger.LogInfo($"[ComputeAvailableColorPresets] VCP 0x14 found: {colorTempVcp != null}");
            if (colorTempVcp != null)
            {
                Logger.LogInfo($"  - ValueList: {(colorTempVcp.ValueList == null ? "NULL" : $"{colorTempVcp.ValueList.Count} items")}");
            }

            // No VCP 0x14 or no values
            if (colorTempVcp == null || colorTempVcp.ValueList == null || colorTempVcp.ValueList.Count == 0)
            {
                Logger.LogWarning($"[ComputeAvailableColorPresets] No VCP 0x14 or empty ValueList - returning empty");
                return new ObservableCollection<ColorPresetItem>();
            }

            // Build preset list from supported values
            var presetList = new List<ColorPresetItem>();
            foreach (var valueInfo in colorTempVcp.ValueList)
            {
                if (int.TryParse(valueInfo.Value?.Replace("0x", string.Empty), System.Globalization.NumberStyles.HexNumber, null, out int vcpValue))
                {
                    var displayName = FormatColorTemperatureDisplayName(valueInfo.Name, vcpValue);
                    presetList.Add(new ColorPresetItem(vcpValue, displayName));
                    Logger.LogDebug($"[ComputeAvailableColorPresets] Added: {displayName}");
                }
            }

            // Sort by VCP value for consistent ordering
            presetList = presetList.OrderBy(p => p.VcpValue).ToList();

            Logger.LogInfo($"[ComputeAvailableColorPresets] COMPLETE - returning {presetList.Count} items");
            Logger.LogInfo($"[ComputeAvailableColorPresets] Current ColorTemperature value: {_colorTemperature}");

            return new ObservableCollection<ColorPresetItem>(presetList);
        }

        /// <summary>
        /// Format color temperature display name
        /// </summary>
        private string FormatColorTemperatureDisplayName(string name, int vcpValue)
        {
            var hexValue = $"0x{vcpValue:X2}";

            // Check if name is undefined (null or empty)
            if (string.IsNullOrEmpty(name))
            {
                return $"Manufacturer Defined ({hexValue})";
            }

            // For predefined names, append the hex value in parentheses
            return $"{name} ({hexValue})";
        }

        /// <summary>
        /// Color presets for display in ComboBox, includes current value if not in preset list
        /// </summary>
        [JsonIgnore]
        public ObservableCollection<ColorPresetItem> ColorPresetsForDisplay
        {
            get
            {
                var presets = AvailableColorPresets;
                if (presets == null || presets.Count == 0)
                {
                    return new ObservableCollection<ColorPresetItem>();
                }

                // Check if current value is in the preset list
                var currentValueInList = presets.Any(p => p.VcpValue == _colorTemperature);

                if (currentValueInList)
                {
                    // Current value is in the list, return as-is
                    return presets;
                }

                // Current value is not in the preset list - add it at the beginning
                var displayList = new List<ColorPresetItem>();

                // Add current value with "Custom" indicator
                var currentValueName = GetColorTemperatureName(_colorTemperature);
                var displayName = string.IsNullOrEmpty(currentValueName)
                    ? $"Custom (0x{_colorTemperature:X2})"
                    : $"{currentValueName} (0x{_colorTemperature:X2}) - Custom";

                displayList.Add(new ColorPresetItem(_colorTemperature, displayName));

                // Add all supported presets
                displayList.AddRange(presets);

                return new ObservableCollection<ColorPresetItem>(displayList);
            }
        }

        /// <summary>
        /// Get the name for a color temperature value from standard VCP naming
        /// </summary>
        private string GetColorTemperatureName(int vcpValue)
        {
            return vcpValue switch
            {
                0x04 => "5000K",
                0x05 => "6500K",
                0x06 => "7500K",
                0x08 => "9300K",
                0x09 => "10000K",
                0x0A => "11500K",
                0x0B => "User 1",
                0x0C => "User 2",
                0x0D => "User 3",
                _ => null,
            };
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
            lines.Add($"VCP Capabilities for {_name}");
            lines.Add($"Monitor: {_name}");
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
            ColorTemperature = other.ColorTemperature;
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

        /// <summary>
        /// Represents a color temperature preset item for VCP code 0x14
        /// </summary>
        public class ColorPresetItem : Observable
        {
            private int _vcpValue;
            private string _displayName = string.Empty;

            [JsonPropertyName("vcpValue")]
            public int VcpValue
            {
                get => _vcpValue;
                set
                {
                    if (_vcpValue != value)
                    {
                        _vcpValue = value;
                        OnPropertyChanged();
                    }
                }
            }

            [JsonPropertyName("displayName")]
            public string DisplayName
            {
                get => _displayName;
                set
                {
                    if (_displayName != value)
                    {
                        _displayName = value;
                        OnPropertyChanged();
                    }
                }
            }

            public ColorPresetItem()
            {
            }

            public ColorPresetItem(int vcpValue, string displayName)
            {
                VcpValue = vcpValue;
                DisplayName = displayName;
            }
        }
    }
}
