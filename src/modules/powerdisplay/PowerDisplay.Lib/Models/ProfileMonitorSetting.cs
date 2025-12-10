// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace PowerDisplay.Common.Models
{
    /// <summary>
    /// Monitor settings for a specific profile
    /// </summary>
    public class ProfileMonitorSetting
    {
        /// <summary>
        /// Gets or sets the monitor's unique identifier.
        /// Format: "{Source}_{EdidId}_{MonitorNumber}" (e.g., "DDC_GSM5C6D_1").
        /// </summary>
        [JsonPropertyName("monitorId")]
        public string MonitorId { get; set; }

        [JsonPropertyName("brightness")]
        public int? Brightness { get; set; }

        [JsonPropertyName("contrast")]
        public int? Contrast { get; set; }

        [JsonPropertyName("volume")]
        public int? Volume { get; set; }

        /// <summary>
        /// Gets or sets the color temperature VCP preset value.
        /// JSON property name kept as "colorTemperature" for backward compatibility.
        /// </summary>
        [JsonPropertyName("colorTemperature")]
        public int? ColorTemperatureVcp { get; set; }

        public ProfileMonitorSetting()
        {
            MonitorId = string.Empty;
        }

        public ProfileMonitorSetting(string monitorId, int? brightness = null, int? colorTemperatureVcp = null, int? contrast = null, int? volume = null)
        {
            MonitorId = monitorId;
            Brightness = brightness;
            ColorTemperatureVcp = colorTemperatureVcp;
            Contrast = contrast;
            Volume = volume;
        }
    }
}
