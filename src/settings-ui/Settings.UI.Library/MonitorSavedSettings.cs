// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// Saved settings for a monitor that can be restored on startup
    /// </summary>
    public class MonitorSavedSettings
    {
        [JsonPropertyName("brightness")]
        public int Brightness { get; set; } = 30;

        [JsonPropertyName("color_temperature")]
        public int ColorTemperature { get; set; } = 6500;

        [JsonPropertyName("contrast")]
        public int Contrast { get; set; } = 50;

        [JsonPropertyName("volume")]
        public int Volume { get; set; } = 50;

        [JsonPropertyName("last_updated")]
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        public MonitorSavedSettings()
        {
        }

        public MonitorSavedSettings(int brightness, int colorTemperature, int contrast, int volume)
        {
            Brightness = brightness;
            ColorTemperature = colorTemperature;
            Contrast = contrast;
            Volume = volume;
            LastUpdated = DateTime.Now;
        }
    }
}