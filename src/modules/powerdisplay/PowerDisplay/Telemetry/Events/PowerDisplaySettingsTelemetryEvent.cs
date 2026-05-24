// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace PowerDisplay.Telemetry.Events
{
    /// <summary>
    /// Telemetry event for PowerDisplay settings
    /// Sent when Runner requests settings telemetry via send_settings_telemetry()
    /// </summary>
    [EventData]
    public class PowerDisplaySettingsTelemetryEvent : EventBase, IEvent
    {
        public new string EventName => "PowerDisplay_Settings";

        public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;

        /// <summary>
        /// Whether the hotkey is enabled
        /// </summary>
        public bool HotkeyEnabled { get; set; }

        /// <summary>
        /// Whether the tray icon is enabled
        /// </summary>
        public bool TrayIconEnabled { get; set; }

        /// <summary>
        /// Number of monitors currently detected
        /// </summary>
        public int MonitorCount { get; set; }

        /// <summary>
        /// Number of profiles saved
        /// </summary>
        public int ProfileCount { get; set; }
    }
}
