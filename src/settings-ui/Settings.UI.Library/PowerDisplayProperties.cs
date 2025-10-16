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
            Theme = "Light";
            BrightnessUpdateRate = "1s";
            Monitors = new List<MonitorInfo>();
            RestoreSettingsOnStartup = true;
            SavedMonitorSettings = new Dictionary<string, MonitorSavedSettings>();
        }

        [JsonPropertyName("launch_at_startup")]
        public bool LaunchAtStartup { get; set; }

        [JsonPropertyName("theme")]
        public string Theme { get; set; }

        [JsonPropertyName("brightness_update_rate")]
        public string BrightnessUpdateRate { get; set; }

        [JsonPropertyName("monitors")]
        public List<MonitorInfo> Monitors { get; set; }

        [JsonPropertyName("restore_settings_on_startup")]
        public bool RestoreSettingsOnStartup { get; set; }

        [JsonPropertyName("saved_monitor_settings")]
        public Dictionary<string, MonitorSavedSettings> SavedMonitorSettings { get; set; }
    }
}
