// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class MonitorInfo : Observable
    {
        private string _name = string.Empty;
        private string _internalName = string.Empty;
        private string _hardwareId = string.Empty;
        private string _communicationMethod = string.Empty;
        private string _monitorType = string.Empty;
        private int _currentBrightness;
        private int _colorTemperature = 6500;
        private bool _isHidden;
        private bool _enableColorTemperature;
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

        // Available color temperature presets (populated from VcpCodesFormatted for VCP 0x14)
        private ObservableCollection<ColorPresetItem> _availableColorPresets = new ObservableCollection<ColorPresetItem>();

        public MonitorInfo()
        {
        }

        public MonitorInfo(string name, string internalName, string communicationMethod)
        {
            Name = name;
            InternalName = internalName;
            CommunicationMethod = communicationMethod;
        }

        public MonitorInfo(string name, string internalName, string hardwareId, string communicationMethod, string monitorType, int currentBrightness, int colorTemperature)
        {
            Name = name;
            InternalName = internalName;
            HardwareId = hardwareId;
            CommunicationMethod = communicationMethod;
            MonitorType = monitorType;
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

        [JsonPropertyName("monitorType")]
        public string MonitorType
        {
            get => _monitorType;
            set
            {
                if (_monitorType != value)
                {
                    _monitorType = value;
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

        [JsonPropertyName("enableColorTemperature")]
        public bool EnableColorTemperature
        {
            get => _enableColorTemperature;
            set
            {
                if (_enableColorTemperature != value)
                {
                    System.Diagnostics.Debug.WriteLine($"[MonitorInfo] EnableColorTemperature changing from {_enableColorTemperature} to {value} for monitor {Name}");
                    _enableColorTemperature = value;
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
                    OnPropertyChanged();
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
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ColorTemperatureTooltip));
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

        [JsonPropertyName("availableColorPresets")]
        public ObservableCollection<ColorPresetItem> AvailableColorPresets
        {
            get => _availableColorPresets;
            set
            {
                if (_availableColorPresets != value)
                {
                    _availableColorPresets = value ?? new ObservableCollection<ColorPresetItem>();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasColorPresets));
                }
            }
        }

        [JsonIgnore]
        public bool HasColorPresets => _availableColorPresets != null && _availableColorPresets.Count > 0;

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
