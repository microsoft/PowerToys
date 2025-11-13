// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
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

        [JsonIgnore]
        public bool HasCapabilities => !string.IsNullOrEmpty(_capabilitiesRaw);
    }
}
