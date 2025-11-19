// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Settings.UI.Library.Attributes;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class PowerDisplayProperties
    {
        [CmdConfigureIgnore]
        public HotkeySettings DefaultActivationShortcut => new HotkeySettings(true, false, false, true, 0x4D); // Win+Alt+M

        public PowerDisplayProperties()
        {
            ActivationShortcut = DefaultActivationShortcut;
            BrightnessUpdateRate = "1s";
            Monitors = new List<MonitorInfo>();
            RestoreSettingsOnStartup = true;
            CurrentProfile = "Custom";

            // Note: saved_monitor_settings has been moved to monitor_state.json
            // which is managed separately by PowerDisplay app
        }

        [JsonPropertyName("activation_shortcut")]
        public HotkeySettings ActivationShortcut { get; set; }

        [JsonPropertyName("brightness_update_rate")]
        public string BrightnessUpdateRate { get; set; }

        [JsonPropertyName("monitors")]
        public List<MonitorInfo> Monitors { get; set; }

        [JsonPropertyName("restore_settings_on_startup")]
        public bool RestoreSettingsOnStartup { get; set; }

        /// <summary>
        /// Current active profile name (e.g., "Custom", "Profile1", "Profile2")
        /// </summary>
        [JsonPropertyName("current_profile")]
        public string CurrentProfile { get; set; }

        /// <summary>
        /// Pending color temperature operation from Settings UI.
        /// This is cleared after PowerDisplay processes it.
        /// </summary>
        [JsonPropertyName("pending_color_temperature_operation")]
        public ColorTemperatureOperation PendingColorTemperatureOperation { get; set; }

        /// <summary>
        /// Pending profile operation from Settings UI.
        /// This is cleared after PowerDisplay processes it.
        /// </summary>
        [JsonPropertyName("pending_profile_operation")]
        public ProfileOperation PendingProfileOperation { get; set; }
    }
}
