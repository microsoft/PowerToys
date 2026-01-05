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
        public HotkeySettings DefaultActivationShortcut => new HotkeySettings(true, true, false, true, 0x44); // Ctrl+Shift+Win+D (win, ctrl, alt, shift, code)

        public PowerDisplayProperties()
        {
            ActivationShortcut = DefaultActivationShortcut;
            MonitorRefreshDelay = 5;
            Monitors = new List<MonitorInfo>();
            RestoreSettingsOnStartup = false;
            ShowSystemTrayIcon = true;
            ShowProfileSwitcher = true;
            ShowIdentifyMonitorsButton = true;
            ShowColorTemperatureSwitcher = false;

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
        /// Gets or sets whether to show the profile switcher button in the flyout UI.
        /// Default is true. When false, the profile switcher is hidden (but profiles still work via Settings).
        /// Note: Also hidden when no profiles exist.
        /// </summary>
        [JsonPropertyName("show_profile_switcher")]
        public bool ShowProfileSwitcher { get; set; }

        /// <summary>
        /// Gets or sets whether to show the identify monitors button in the flyout UI.
        /// Default is true.
        /// </summary>
        [JsonPropertyName("show_identify_monitors_button")]
        public bool ShowIdentifyMonitorsButton { get; set; }

        /// <summary>
        /// Gets or sets whether to show the color temperature switcher in the flyout UI.
        /// Default is false. When enabled, shows a color temperature preset picker for each monitor.
        /// </summary>
        [JsonPropertyName("show_color_temperature_switcher")]
        public bool ShowColorTemperatureSwitcher { get; set; }

        /// <summary>
        /// Gets or sets pending profile operation from Settings UI.
        /// This is cleared after PowerDisplay processes it.
        /// </summary>
        [JsonPropertyName("pending_profile_operation")]
        public ProfileOperation PendingProfileOperation { get; set; }
    }
}
