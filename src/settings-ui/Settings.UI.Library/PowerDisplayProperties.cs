// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using PowerDisplay.Models;
using Settings.UI.Library.Attributes;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class PowerDisplayProperties
    {
        [CmdConfigureIgnore]
        public HotkeySettings DefaultActivationShortcut => new HotkeySettings(true, true, false, true, 0x50); // Win+Ctrl+Shift+P (win, ctrl, alt, shift, code)

        public PowerDisplayProperties()
        {
            ActivationShortcut = DefaultActivationShortcut;
            MonitorRefreshDelay = 5;
            Monitors = new List<MonitorInfo>();
            RestoreSettingsOnStartup = false;
            ShowSystemTrayIcon = true;
            ShowProfileSwitcher = true;
            ShowIdentifyMonitorsButton = true;
            MaxCompatibilityMode = false;
            LinkedLevelsActive = false;
            ExcludedFromSyncMonitorIds = new List<string>();
            CustomVcpMappings = new List<CustomVcpValueMapping>();

            // Note: saved_monitor_settings has been moved to monitor_state.json
            // which is managed separately by PowerDisplay app
        }

        private HotkeySettings _activationShortcut;

        [JsonPropertyName("activation_shortcut")]
        public HotkeySettings ActivationShortcut
        {
            get => _activationShortcut ?? DefaultActivationShortcut;
            set => _activationShortcut = value;
        }

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

        /// <summary>
        /// Gets or sets a value indicating whether PowerDisplay should aggressively probe each
        /// supported VCP feature when a monitor's DDC/CI capabilities string is empty or
        /// unparsable. Disabled by default; enabling it increases monitor discovery time but
        /// can surface monitors whose firmware does not advertise capabilities correctly.
        /// </summary>
        [JsonPropertyName("max_compatibility_mode")]
        public bool MaxCompatibilityMode { get; set; }

        [JsonPropertyName("show_system_tray_icon")]
        public bool ShowSystemTrayIcon { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to show the profile switcher button in the flyout UI.
        /// Default is true. When false, the profile switcher is hidden (but profiles still work via Settings).
        /// Note: Also hidden when no profiles exist.
        /// </summary>
        [JsonPropertyName("show_profile_switcher")]
        public bool ShowProfileSwitcher { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to show the identify monitors button in the flyout UI.
        /// Default is true.
        /// </summary>
        [JsonPropertyName("show_identify_monitors_button")]
        public bool ShowIdentifyMonitorsButton { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether brightness slider changes are broadcast
        /// to all connected monitors as a single linked level. When false (default), each
        /// monitor's slider operates independently. The toggle is only meaningful when two
        /// or more monitors are connected; the UI hides the entry point otherwise.
        /// </summary>
        [JsonPropertyName("linked_levels_active")]
        public bool LinkedLevelsActive { get; set; }

        /// <summary>
        /// Gets or sets the set of monitor <c>Id</c> values excluded from linked brightness.
        /// Keyed by <c>Monitor.Id</c> (the DevicePath-based identifier, unique per physical
        /// device × port — the same key profiles use), so three identical monitors of the same
        /// model are distinguished. An excluded monitor keeps its own independent brightness
        /// slider while link mode is on. Monitors not present here are linked by default,
        /// including newly connected ones.
        /// </summary>
        [JsonPropertyName("excluded_from_sync_monitor_ids")]
        public List<string> ExcludedFromSyncMonitorIds { get; set; }

        /// <summary>
        /// Gets or sets custom VCP value name mappings shared across all monitors.
        /// Allows users to define custom names for color temperature presets and input sources.
        /// </summary>
        [JsonPropertyName("custom_vcp_mappings")]
        public List<CustomVcpValueMapping> CustomVcpMappings { get; set; }
    }
}
