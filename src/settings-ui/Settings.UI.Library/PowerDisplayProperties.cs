// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class PowerDisplayProperties
    {
        public PowerDisplayProperties()
        {
            LaunchAtStartup = false;
            BrightnessUpdateRate = "1s";
            Monitors = new List<MonitorInfo>();
            RestoreSettingsOnStartup = true;
            // Note: saved_monitor_settings has been moved to monitor_state.json
            // which is managed separately by PowerDisplay app
        }

        [JsonPropertyName("launch_at_startup")]
        public bool LaunchAtStartup { get; set; }

        [JsonPropertyName("brightness_update_rate")]
        public string BrightnessUpdateRate { get; set; }

        [JsonPropertyName("monitors")]
        public List<MonitorInfo> Monitors { get; set; }

        [JsonPropertyName("restore_settings_on_startup")]
        public bool RestoreSettingsOnStartup { get; set; }
    }
}
