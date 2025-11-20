// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// Monitor settings for a specific profile
    /// </summary>
    public class ProfileMonitorSetting
    {
        [JsonPropertyName("hardwareId")]
        public string HardwareId { get; set; }

        [JsonPropertyName("brightness")]
        public int? Brightness { get; set; }

        [JsonPropertyName("contrast")]
        public int? Contrast { get; set; }

        [JsonPropertyName("volume")]
        public int? Volume { get; set; }

        [JsonPropertyName("colorTemperature")]
        public int? ColorTemperature { get; set; }

        public ProfileMonitorSetting()
        {
            HardwareId = string.Empty;
        }

        public ProfileMonitorSetting(string hardwareId, int? brightness = null, int? colorTemperature = null, int? contrast = null, int? volume = null)
        {
            HardwareId = hardwareId;
            Brightness = brightness;
            ColorTemperature = colorTemperature;
            Contrast = contrast;
            Volume = volume;
        }
    }
}
