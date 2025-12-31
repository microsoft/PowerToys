// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using PowerDisplay.Common.Models;
using Settings.UI.Library.Attributes;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class PowerDisplayProperties
    {
        [CmdConfigureIgnore]
        public HotkeySettings DefaultActivationShortcut => new HotkeySettings(true, true, false, true, 0x4D); // Ctrl+Shift+Win+M (win, ctrl, alt, shift, code)

        public PowerDisplayProperties()
        {
            ActivationShortcut = DefaultActivationShortcut;
            MonitorRefreshDelay = 5;
            Monitors = new List<MonitorInfo>();
            RestoreSettingsOnStartup = false;
            ShowSystemTrayIcon = true;

            // Note: saved_monitor_settings has been moved to monitor_state.json
            // which is managed separately by PowerDisplay app
        }

        [JsonPropertyName("activation_shortcut")]
        public HotkeySettings ActivationShortcut { get; set; }

        /// <summary>
        /// Gets or sets delay in seconds before refreshing monitors after display changes (hot-plug).
        /// This allows hardware to stabilize before querying DDC/CI.
        /// </summary>
        [JsonPropertyName("monitor_refresh_delay")]
        public int MonitorRefreshDelay { get; set; }

        [JsonPropertyName("monitors")]
        public List<MonitorInfo> Monitors { get; set; }

        [JsonPropertyName("restore_settings_on_startup")]
        public bool RestoreSettingsOnStartup { get; set; }

        [JsonPropertyName("show_system_tray_icon")]
        public bool ShowSystemTrayIcon { get; set; }

        /// <summary>
        /// Gets or sets pending color temperature operation from Settings UI.
        /// This is cleared after PowerDisplay processes it.
        /// </summary>
        [JsonPropertyName("pending_color_temperature_operation")]
        public ColorTemperatureOperation PendingColorTemperatureOperation { get; set; }

        /// <summary>
        /// Gets or sets pending profile operation from Settings UI.
        /// This is cleared after PowerDisplay processes it.
        /// </summary>
        [JsonPropertyName("pending_profile_operation")]
        public ProfileOperation PendingProfileOperation { get; set; }
    }
}
