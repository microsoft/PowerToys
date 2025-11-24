// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Utils;

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
                    OnPropertyChanged(nameof(BrightnessTooltip));
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
                    OnPropertyChanged(nameof(ContrastTooltip));
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
                    OnPropertyChanged(nameof(ColorTemperatureTooltip));
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
                    OnPropertyChanged(nameof(VolumeTooltip));
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
            {
                if (int.TryParse(v.Code?.Replace("0x", string.Empty), System.Globalization.NumberStyles.HexNumber, null, out int code))
                {
                    return code == ColorTemperatureHelper.ColorTemperatureVcpCode;
                }

                return false;
            });

            // No VCP 0x14 or no values
            if (colorTempVcp == null || colorTempVcp.ValueList == null || colorTempVcp.ValueList.Count == 0)
            {
                return new ObservableCollection<ColorPresetItem>();
            }

            // Extract VCP values as tuples for ColorTemperatureHelper
            var colorTempValues = colorTempVcp.ValueList
                .Select(valueInfo =>
                {
                    int.TryParse(valueInfo.Value?.Replace("0x", string.Empty), System.Globalization.NumberStyles.HexNumber, null, out int vcpValue);
                    return (VcpValue: vcpValue, Name: valueInfo.Name);
                })
                .Where(x => x.VcpValue > 0);

            // Use shared helper to compute presets, then convert to nested type for XAML compatibility
            var basePresets = ColorTemperatureHelper.ComputeColorPresets(colorTempValues);
            var presetList = basePresets.Select(p => new ColorPresetItem(p.VcpValue, p.DisplayName));
            return new ObservableCollection<ColorPresetItem>(presetList);
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

                // Add current value with "Custom" indicator using shared helper
                var displayName = ColorTemperatureHelper.FormatCustomColorTemperatureDisplayName(_colorTemperature);
                displayList.Add(new ColorPresetItem(_colorTemperature, displayName));

                // Add all supported presets
                displayList.AddRange(presets);

                return new ObservableCollection<ColorPresetItem>(displayList);
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

        [JsonIgnore]
        public string ColorTemperatureTooltip => _supportsColorTemperature ? string.Empty : "Color temperature control not supported by this monitor";

        [JsonIgnore]
        public string VolumeTooltip => _supportsVolume ? string.Empty : "Volume control not supported by this monitor";

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
